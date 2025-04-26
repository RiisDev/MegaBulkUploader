using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MegaBulkUploader.Modules.Misc;
using MegaBulkUploader.Modules.Output;
using static System.Text.RegularExpressions.Regex;
#pragma warning disable SYSLIB1045

namespace MegaBulkUploader.Modules.Clients
{
    public class MegaCliWrapper
    {
        private ProgressBar? _progressBar;

        private readonly int _fileCount;
        private int _finished;
        private int _lastProgress;

        private bool _windowsCmdServerLoading = true;

        private readonly string? _token;

        private readonly TaskCompletionSource _transfersFinished = new();

        public MegaCliWrapper(string authToken, int fileCount)
        {
            _fileCount = fileCount;
            _token = authToken;

            DeleteCache().Wait();
        }

        public static void KillMegaProcesses()
        {
            foreach (Process proc in Process.GetProcesses())
            {
                if (!proc.ProcessName.Contains("mega", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (proc.Id == Environment.ProcessId) continue;
                proc.Kill(true);
            }
        }

        public bool ShouldRetry(string program, string expectedResponse, string dataActual)
        {
            bool isWhoAmIRetry = program == "whoami" && !dataActual.Contains(expectedResponse);
            
            bool isErrorResponse = dataActual.Contains(":err:") && !(program == "export" && dataActual.Contains("Nodes not found"));

            bool isServerFailure = dataActual.Contains("Failed to access server");

            return isWhoAmIRetry || isErrorResponse || isServerFailure;
        }

        private void ProcessExportedData(string dataActual, ref TaskCompletionSource responseFound)
        {
            Match exportedLink = Match(dataActual, @"(https:\/\/mega\.nz\/folder\/(.*))", RegexOptions.Compiled);
            Program.Exported.Add(exportedLink.ToString().Trim());
            responseFound.TrySetResult();
        }

        public record TransferData(string Source, string Destination, double Progress, string Status, string Size);

        public static List<TransferData> GetActiveProgress(string input)
        {
            List<TransferData> transfers = [];
            string[] lines = input.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (!line.Contains("ACTIVE") && !line.Contains("QUEUED")) continue;

                MatchCollection groupMatch = Matches(line, @"([^*]+)");
                if (groupMatch.Count <= 0) continue;

                Match percentMatch = Match(line, @"(\d+\.\d+)%");
                if (!percentMatch.Success) continue;

                Match sizeMatch = Match(line, @"of\s*(\d+(?:\.\d+)?\s*(?:[KMGTPBEZY]?B|B|bytes))");
                string size = sizeMatch.Success ? sizeMatch.Groups[1].Value : "0B";

                bool percent = double.TryParse(percentMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double percentage);
                if (!percent) continue;

                transfers.Add(new TransferData(
                    Source: groupMatch[2].Value, 
                    Destination: groupMatch[3].Value, 
                    Progress: percentage, 
                    Status: groupMatch[5].Value,
                    size.Replace(" ", "")
                ));
            }

            return transfers;
        }

        public async Task StartTransfersMonitor()
        {
            while (true)
            {
                if (OperatingSystem.IsWindows())
                    if (Process.GetProcessesByName("MEGAcmdServer").Length == 0) continue;

                TaskCompletionSource processClosed = new();
                ProcessStartInfo startInfo = new()
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = $"{(OperatingSystem.IsWindows() ? "/C \"\"" : "-c \"")}"
                };

                List<string> arguments =
                [
                    OperatingSystem.IsWindows()
                        ? $"{StaticModules.GetCli()}\" transfers"
                        : $"{StaticModules.GetCli()} transfers",
                    "--limit=21474836",
                    "--path-display-size=255",
                    "--col-separator=*"
                ];

                startInfo.Arguments += string.Join(" ", arguments).Trim() + "\"";

                Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true, };

                process.Exited += (_, _) => processClosed.SetResult();

                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string errorOutput = await process.StandardError.ReadToEndAsync();

                await processClosed.Task;

                Debug.WriteLine(output);
                Debug.WriteLine(errorOutput);

                List<TransferData> currentProgress = GetActiveProgress(output);

                // Linux is special where it shows active transfers as 100 and active while processing instead of switching status, so we want to skip them
                TransferData? activeData = OperatingSystem.IsWindows()
                    ? currentProgress.LastOrDefault(x =>
                        x.Status.Contains("active", StringComparison.InvariantCultureIgnoreCase))
                    : currentProgress.LastOrDefault(x =>
                        x.Status.Contains("active", StringComparison.InvariantCultureIgnoreCase) &&
                        x.Progress.ToString(CultureInfo.InvariantCulture) != "100.00");

                if (activeData is null && _finished >= _fileCount) { _transfersFinished.SetResult(); _progressBar?.Dispose(); break; }
                if (activeData is null && _finished == _fileCount - 1 && currentProgress.Count == 0) { _transfersFinished.SetResult(); _progressBar?.Dispose(); break; }
                if (activeData is null) continue;
                if (activeData.Progress < _lastProgress) _finished++;

                _lastProgress = (int)activeData.Progress;
                _progressBar?.Report((activeData, _finished + 1, _fileCount));

                if (_finished >= _fileCount) { _transfersFinished.SetResult(); _progressBar?.Dispose(); }
                else { await Task.Delay(500); continue; }

                break;
            }
        }

        public async Task ExecuteCli(string program, string expectedResponse, params string[] args)
        {
            TaskCompletionSource processClosed = new();
            TaskCompletionSource responseFound = new();

            ProcessStartInfo startInfo = new()
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"{(OperatingSystem.IsWindows() ? "/C \"\"" : "-c \"")}"
            };

            List<string> arguments =
            [
                OperatingSystem.IsWindows()
                        ? $"{StaticModules.GetCli()}\" {program}"
                        : $"{StaticModules.GetCli()} {program}"
            ];

            if (args.Length > 0)
                arguments.Add(string.Join(" ", args));

            startInfo.Arguments += string.Join(" ", arguments).Trim();

            if (args.Length == 0) startInfo.Arguments += "\"";
            else if (program == "https" || program == "speedlimit") startInfo.Arguments += "\"";
            else startInfo.Arguments += "\"\"";

            Debug.WriteLine($"{startInfo.FileName} {startInfo.Arguments}");

            if (OperatingSystem.IsWindows())
            {
                startInfo.WorkingDirectory = Path.GetDirectoryName(StaticModules.GetCli());

                if (Process.GetProcessesByName("MEGAcmdServer").Length == 0)
                {
                    Process cmdServer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C \"MEGAcmdServer.exe psa --discard update --auto=off\"",
                        WorkingDirectory = Path.GetDirectoryName(StaticModules.GetCli()),
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })!;

                    cmdServer.OutputDataReceived += (_, e) => { Debug.WriteLine(e.Data); if (e.Data?.Contains("Listening to petitions") ?? false) _windowsCmdServerLoading = false; };

                    cmdServer.BeginOutputReadLine();

                    while (_windowsCmdServerLoading) await Task.Delay(100);
                }
            }

            if (program == "put")
                startInfo.Arguments = startInfo.Arguments[..^1];

            Process process = new()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            process.Exited += (_, _) => processClosed.SetResult();

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string errorOutput = await process.StandardError.ReadToEndAsync();

            await processClosed.Task;

            Debug.WriteLine(output);
            Debug.WriteLine(errorOutput);

            if(ShouldRetry(program, expectedResponse, output))
            {
                await Task.Delay(500);
                await ExecuteCli(program, expectedResponse, args);
            }
            else if (output.Contains("Exported")) ProcessExportedData(output, ref responseFound);
            else if (output.Contains(expectedResponse)) responseFound.TrySetResult();

            process.Dispose();
        }

        public static Task DeleteCache()
        {
            try
            {
                KillMegaProcesses();

                if (OperatingSystem.IsWindows())
                {
                    string directory = Path.GetDirectoryName(StaticModules.GetCli()) + "\\.megaCmd";

                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                else if (OperatingSystem.IsLinux())
                {
                    if (!Directory.Exists("/home"))
                        throw new DirectoryNotFoundException("Failed to find /home directory.");

                    foreach (string dir in Directory.GetDirectories("/home"))
                        if (Directory.Exists($"{dir}/.megaCmd"))
                            Directory.Delete($"{dir}/.megaCmd", true);
                }
                else
                    throw new NotSupportedException("Unsupported OS");
            }
            catch (Exception ex)
            {
                new Log("Cache").LogInformation($"Failed to clear cache: {ex}");
            }

            return Task.CompletedTask;
        }

        public async Task<string> ExtractVerificationEmail()
        {
            string extractedConfirmLink = "";
            bool foundEmail = false;
            while (true)
            {
                if (foundEmail) break;
                foreach ((string _, string text) in await MailClient.GetEmails(_token!))
                {
                    if (!text.Contains("https://mega.nz/#confirm")) continue;

                    Match regexMatch = Match(text, "href\\s*=\\s*\"(https:\\/\\/mega\\.nz\\/#confirm[^\"\"]*)\"", RegexOptions.Compiled);

                    extractedConfirmLink = regexMatch.Groups[1].Value;
                    foundEmail = true;
                }
            }

            return extractedConfirmLink;
        }

        public async Task Register(string email, string password, string name, string directory)
        {
            Log megaLogger = new("MegaCli");
            megaLogger.LogInformation("Clearing mega cache...");
            await DeleteCache();

            megaLogger.LogInformation("Creating mega account...");
            await ExecuteCli("signup", "You will receive a confirmation link", $"\"{email}\"", $"\"{password}\"", $"\"{name}");

            megaLogger.LogInformation("Waiting for verification email...");
            string emailVerificationLink = await ExtractVerificationEmail();

            megaLogger.LogInformation("Confirming mega account...");
            await ExecuteCli("confirm", "You can login with it now", $"\"{emailVerificationLink}\"", $"\"{email}\"", $"\"{password}");

            megaLogger.LogInformation("Signing into mega account...");
            await ExecuteCli("login", "", $"\"{email}\"", $"\"{password}");
            await ExecuteCli("whoami", email);
            
            KillMegaProcesses(); // I found it had more reliability when resetting the command server, and clear up any extra exec instances

            megaLogger.LogInformation("Successfully created mega account!");

        }

        public async Task DoPutUpload(Log megaLogger, IReadOnlyList<string> files, string directory, int uploadStreams)
        {
            megaLogger.LogInformation("Disabling https (Increases upload speed)");
            await ExecuteCli("https", "File transfer now uses HTTP", "off");
            await ExecuteCli("speedlimit", "", $"--upload-connections {uploadStreams}");

            _ = Task.Run(async () =>
            {
                megaLogger.LogInformation("Monitoring transfers...");
                _progressBar = new ProgressBar();
                await StartTransfersMonitor();
            });

            megaLogger.LogInformation("Creating upload queue, this will take a while depending on file count...");

            foreach (string file in files)
            {
                string uploadDirectory = "";
                int locationIndex = file.IndexOf(directory, StringComparison.InvariantCultureIgnoreCase);

                if (locationIndex < 0)
                    uploadDirectory += $"{directory}/{file}";
                else
                    uploadDirectory += file[locationIndex..];

                if (OperatingSystem.IsWindows()) uploadDirectory = uploadDirectory.Replace("\\", "/");
                if (OperatingSystem.IsLinux()) uploadDirectory = uploadDirectory[1..];

                await ExecuteCli("put", "", "-c", "-q", "--ignore-quota-warn", $"\"{file}\"", $"\"{uploadDirectory}");
            }

            await _transfersFinished.Task;

            _progressBar?.Dispose();

            megaLogger.LogInformation("Uploading complete!");
            megaLogger.LogInformation("Extracting folder url...");

            await ExecuteCli("export", "Exported", "-a", "-f", $"\"/{directory}");
        }

        public async Task UploadFiles(IReadOnlyList<string> files, string directory, int uploadStreams)
        {
            Log megaLogger = new("MegaCli");
            
            await DoPutUpload(megaLogger, files, directory, uploadStreams);

            megaLogger.LogInformation($"Exported Url: {Program.Exported.Last()}");
            megaLogger.LogInformation("Cleaning up...");

            await DeleteCache();
        }
    }
}
