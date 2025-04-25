using MegaBulkUploader.Modules.Output;
using System.Diagnostics;
using MegaBulkUploader.Modules.Clients;
using MegaBulkUploader.Modules.FolderProcessing;

namespace MegaBulkUploader
{
    internal class Program
    {
        public static readonly List<string> Exported = [];
        
        public static readonly Random Random = new();
        public static string RandomString(int length) { return new string(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length).Select(s => s[Random.Next(s.Length)]).ToArray()); }
        
        public static bool IsAdmin()
        {
            if (!OperatingSystem.IsLinux()) return true; // Only linux needs admin due to .megaCmd folder

            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "sudo",
                    Arguments = "-n true",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? process = Process.Start(psi);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static string GetCli()
        {
            if (OperatingSystem.IsWindows())
            {
                return $@"{AppDomain.CurrentDomain.BaseDirectory}cli{(Environment.Is64BitOperatingSystem ? "\\x64" : "\\x86")}\MEGAClient.exe";
            }

            if (OperatingSystem.IsLinux())
            {
                return "/usr/bin/mega-exec";
            }

            throw new NotSupportedException("Unsupported OS");
        }

        static void Cleanup(object? o, EventArgs eventArgs)
        {
            Log logger = new("Init");
            logger.LogInformation("Process exited.");
            logger.LogInformation("Cleaning up...");
            MegaCliWrapper.KillMegaProcesses();
            MegaCliWrapper.DeleteCache();
        }

        static void Main(string[] args)
        {
            Log logger = new("Init");
            logger.LogInformation("Starting Process...");
            logger.LogInformation("Checking for folder to upload...");

            AppDomain.CurrentDomain.ProcessExit += Cleanup;
            Console.CancelKeyPress += Cleanup;

            if (args.Length == 0 || (!Directory.Exists(args[0]) && !File.Exists(args[0])))
            {
                logger.LogError("Invalid directory or file.");
                return;
            }

            logger.LogInformation("Checking for CLI Tool...");

            if (!File.Exists(GetCli()))
            {
                logger.LogError("MegaCLI not found.");
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                if (!IsAdmin())
                {
                    logger.LogError("Please run as sudo.");
                    return;
                }
            }

            _ = Task.Run(() => Processing.Process(args[0]));

            while (true) { Console.ReadLine(); }
        }
    }
}
