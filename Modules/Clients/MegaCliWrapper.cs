using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MegaBulkUploader.Modules.Output;
using static System.Text.RegularExpressions.Regex;
#pragma warning disable SYSLIB1045

namespace MegaBulkUploader.Modules.Clients
{
    public class MegaCliWrapper
    {
        private ProgressBar? _progressBar;
        public static CancellationTokenSource Finished = new();

        private readonly int _fileCount;
        private int _clientFound;
        private int _finished;
        private int _lastProgress;

        private bool _windowsCmdServerLoading = true;

        private readonly string? _token;

        public List<string> ExportedLinks = [];


        public MegaCliWrapper(string authToken, int fileCount)
        {
            _fileCount = fileCount;
            _token = authToken;

            KillMegaProcesses();

            Task.Run(async () =>
            {
                while (!Finished.IsCancellationRequested)
                {
                    await Task.Delay(50);
                    _clientFound = Process.GetProcesses().Count(x => x.ProcessName.Contains("mega", StringComparison.CurrentCultureIgnoreCase) && x.Id != Environment.ProcessId);
                }
            });
        }

        public static void KillMegaProcesses(string error = "", bool exit = false)
        {
            if (!string.IsNullOrEmpty(error))
                new Log("MegaSupport").LogError(error);

            foreach (Process proc in Process.GetProcesses())
            {
                if (!proc.ProcessName.Contains("mega", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (proc.Id == Environment.ProcessId) continue;
                proc.Kill(true);
            }

            if (exit) Environment.Exit(0);
        }

        public bool ShouldRetry(string program, string expectedResponse, string dataActual)
        {
            bool isWhoAmIRetry = program == "whoami" && dataActual == "" && expectedResponse == "Not logged in";

            bool isErrorResponse = dataActual.Contains(":err:") && !(program == "whoami" && expectedResponse == "Not logged in") &&
                                   !(program == "export" && dataActual.Contains("Nodes not found"));

            bool isServerFailure = dataActual.Contains("Failed to access server");

            return isWhoAmIRetry || isErrorResponse || isServerFailure;
        }

        private void ProcessExportedData(string dataActual, ref TaskCompletionSource responseFound)
        {
            Match exportedLink = Match(dataActual, @"(https:\/\/mega\.nz\/folder\/(.*))", RegexOptions.Compiled);
            ExportedLinks.Add(exportedLink.ToString());
            responseFound.TrySetResult();
        }

        private async Task LogoutProper()
        {
            KillMegaProcesses();
            await Task.Delay(250);
            await ExecuteCli("logout", "Logging out");
            await Task.Delay(1000);
            KillMegaProcesses();

            DeleteCache();
        }
        public static double GetActiveProgress(string input)
        {
            string[] lines = input.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            foreach (string line in lines)
            {
                if (!line.Contains("ACTIVE")) continue;

                Match match = Match(line, @"(\d+\.\d+)%");
                if (!match.Success) continue;

                if (double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    return result;
            }

            return 0.0;
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
                        ? $"{Program.GetCli()}\" {program}" // Windows formatting: cli/x64/MEGAClient.exe <program_name>
                        : $"{Program.GetCli()}{program}" // Linux formatting: /usr/bin/mega-<program_name>
            ];

            if (args.Length > 0)
                arguments.Add(string.Join(" ", args));

            startInfo.Arguments += string.Join(" ", arguments).Trim();
            startInfo.Arguments += "\"\"";

            if (OperatingSystem.IsWindows())
            {
                startInfo.WorkingDirectory = Path.GetDirectoryName(Program.GetCli());

                if (Process.GetProcessesByName("MEGAcmdServer").Length == 0)
                {
                    Process cmdServer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C \"MEGAcmdServer.exe psa --discard update --auto=off\"",
                        WorkingDirectory = Path.GetDirectoryName(Program.GetCli()),
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

            if (program == "transfers")
            {
                if (output.Contains("DESTINYPATH"))
                {
                    double currentProgress = GetActiveProgress(output);

                    if (currentProgress < _lastProgress)
                        _finished++;

                    _lastProgress = (int)currentProgress;
                    _progressBar?.Report((currentProgress, _finished + 1, _fileCount));
                    Debug.WriteLine($"{currentProgress} | {_finished + 1}/{_fileCount}");

                    if (_finished >= _fileCount)
                    {
                        responseFound.TrySetResult();
                        _progressBar?.Dispose();
                        Console.WriteLine('\n');
                    }
                    else
                    {
                        await Task.Delay(500);

                        if (_clientFound >= 3) return;

                        await ExecuteCli(program, expectedResponse, args);
                    }
                }
            }
            else if(ShouldRetry(program, expectedResponse, output))
            {
                await Task.Delay(1000);
                await ExecuteCli(program, expectedResponse, args);
            }
            else if (output.Contains("Failed to logout"))
            {
                KillMegaProcesses("Failed to logout of session, please restart application!", true);
            }
            else if (output.Contains("Exported"))
            {
                ProcessExportedData(output, ref responseFound);
            }
            else if (output.Contains(expectedResponse))
            {
                responseFound.TrySetResult();
            }

            process.Dispose();
        }


        public static void DeleteCache()
        {
            KillMegaProcesses();

            if (OperatingSystem.IsWindows())
            {
                if (Directory.Exists(Path.GetDirectoryName($"{Program.GetCli()}\\.megaCmd")!))
                    Directory.Delete(Path.GetDirectoryName($"{Program.GetCli()}\\.megaCmd")!, true);
            }
            else if (OperatingSystem.IsLinux())
            {
                if (!Directory.Exists("/home"))
                    throw new DirectoryNotFoundException("Failed to find /home directory.");

                foreach (string dir in Directory.GetDirectories("/home"))
                {
                    if (Directory.Exists($"{dir}/.megaCmd"))
                    {
                        Directory.Delete($"{dir}/.megaCmd", true);
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unsupported OS");
            }
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
            megaLogger.LogInformation("Logging out of existing mega accounts...");
            await LogoutProper();

            megaLogger.LogInformation("Creating mega account...");
            await ExecuteCli("signup", "You will receive a confirmation link", $"\"{email}\"", $"\"{password}\"", $"\"{name}");

            megaLogger.LogInformation("Waiting for verification email...");
            string emailVerificationLink = await ExtractVerificationEmail();

            megaLogger.LogInformation("Confirming mega account...");
            await ExecuteCli("confirm", "You can login with it now", $"\"{emailVerificationLink}\"", $"\"{email}\"", $"\"{password}");

            megaLogger.LogInformation("Signing into mega account...");
            await ExecuteCli("login", "", $"\"{email}\"", $"\"{password}");

            await Task.Delay(3000);

            await ExecuteCli("whoami", "Account e-mail:");
            
            KillMegaProcesses();

            megaLogger.LogInformation("Successfully created mega account!");

        }

        public async Task DoPutUpload(Log megaLogger, IReadOnlyList<string> files, string directory)
        {
            megaLogger.LogInformation("Disabling https (Increases upload speed)");
            await ExecuteCli("https", "File transfer now uses HTTP", "off");

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

                await ExecuteCli("put", "", "-c", "-q", "--ignore-quota-warn", $"\"{file}\"", $"\"{uploadDirectory}");
            }

            megaLogger.LogInformation("Monitoring transfers...");
            _progressBar = new ProgressBar();

            await ExecuteCli("transfers", "");
            
            _progressBar?.Dispose();

            megaLogger.LogInformation("Uploading complete!");
            megaLogger.LogInformation("Extracting folder url...");

            await ExecuteCli("export", "Exported", "-a", "-f", $"\"/{directory}");
        }

        public async Task UploadFiles(IReadOnlyList<string> files, string directory)
        {
            Log megaLogger = new("MegaCli");

            await Task.Delay(1000);
            
            await DoPutUpload(megaLogger, files, directory);

            megaLogger.LogInformation($"Exported Url: {ExportedLinks.Last()}");

            megaLogger.LogInformation("Cleaning up...");
            await LogoutProper();

            await Task.Delay(2000);

            KillMegaProcesses();
            DeleteCache();
            await Finished.CancelAsync();
        }
    }
}
