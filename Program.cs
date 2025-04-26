using MegaBulkUploader.Modules.Output;
using MegaBulkUploader.Modules.Clients;
using MegaBulkUploader.Modules.Misc;
using System;
using System.Diagnostics;

namespace MegaBulkUploader
{
    internal class Program
    {
        public static readonly List<string> Exported = [];
        
        public static readonly Random Random = new();
        public static string RandomString(int length) { return new string(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", length).Select(s => s[Random.Next(s.Length)]).ToArray()); }

        private const string HelpString = """
                                          Usage:
                                            dotnet app.dll <pathToUpload> [options]

                                          Arguments:
                                            pathToUpload                Required. Path to the file or directory to upload.

                                          Options:
                                            -h, --help                  Show this help message and exit.
                                            -si, --start-index <n>      Starting section index (default: 0).
                                            -ms, --max-size <bytes>     Maximum size in bytes per split chunk (default: 19327352832 / 20 GB).
                                            -us, --upload-streams <n>   Number of parallel upload streams to use (default: 6, max: 6, min: 1).
                                            -o, --output                File to output the upload log (default: Upload.log).
                                            -bb, --bbcode               Output bbCode formatted log (default: false).
                                            -bo, --bbcode-out           File to output bbCode formatted log (default: BbUpload.log).

                                          Example:
                                            dotnet MegaBulkUploader.dll ./my-folder --start-index 0 --max-size 19327352832 --upload-streams 6
                                          """;
        
        static void Main(string[] args)
        {
            MegaCliWrapper.DeleteCache().Wait();

            AppDomain.CurrentDomain.ProcessExit += StaticModules.Cleanup;
            Console.CancelKeyPress += StaticModules.Cleanup;
            
            if (args.Length == 0) args = ["--help"];

            CliParse parser = new(args, aliases: new Dictionary<string, string>
            {
                {"h", "help"},
                {"si", "start-index"},
                {"ms", "max-size"},
                {"us", "upload-streams"},
                {"o", "output"},
                {"bb", "bbcode"},
                {"bo", "bbcode-out"}
            });

            if (parser.HasFlag("help")) { Console.WriteLine(HelpString); return; }

            Log logger = new("Init");
            logger.LogInformation("Starting Process...");
            logger.LogInformation("Checking for folder to upload...");

            if (!Directory.Exists(args[0]) && !File.Exists(args[0]))
            {
                logger.LogError(args.Length == 0 ? "Path missing to upload." : $"{args[0]} is an invalid directory or file.");
                return;
            }

            logger.LogInformation("Checking for CLI Tool...");

            if (!File.Exists(StaticModules.GetCli()))
            {
                logger.LogError("MegaCLI not found.");
                return;
            }

#if DEBUG
#else
            if (OperatingSystem.IsLinux())
            {
                if (!StaticModules.IsAdmin())
                {
                    logger.LogError("Please run as sudo.");
                    return;
                }
            }
#endif
            
            _ = Task.Run(() => FileSystem.Process(new FileSystem.Settings(
                Path: args[0],
                SectionIndex: int.Parse(parser.GetArgument("start-index")?.Trim() ?? "0"),
                SplitSize: long.Parse(parser.GetArgument("max-size")?.Trim() ?? "19327352832"),
                UploadStreams: int.Parse(parser.GetArgument("upload-streams")?.Trim() ?? "6"),
                OutputFile: parser.GetArgument("output") ?? "Upload.log",
                BbOutputFile: parser.GetArgument("bbcode-out") ?? "BbUpload.log",
                OutputBbFile: parser.HasFlag("bbcode") || parser.GetArgument("bbcode")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            )));

            while (true) { Console.ReadLine(); }
        }
    }
}
