using MegaBulkUploader.Modules.Clients;
using MegaBulkUploader.Modules.Output;
using System.Diagnostics;

namespace MegaBulkUploader.Modules.Misc
{
    public static class StaticModules
    {
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
	            return $@"{AppDomain.CurrentDomain.BaseDirectory}cli{(Environment.Is64BitOperatingSystem ? "\\x64" : "\\x86")}\MEGAClient.exe";

            return OperatingSystem.IsLinux() ? "/usr/bin/mega-exec" : throw new NotSupportedException("Unsupported OS");
        }

        public static void Cleanup(object? o, EventArgs eventArgs)
        {
	        Console.CursorVisible = true;
            Console.ResetColor();
            
            Log logger = new("Init");
            logger.LogInformation("Process exited.");
            logger.LogInformation("Cleaning up...");
            MegaCliWrapper.DeleteCache().Wait();
        }
    }
}
