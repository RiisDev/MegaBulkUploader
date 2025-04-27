using System.Diagnostics.CodeAnalysis;
using MegaBulkUploader.Modules.Output;
using System.Text;

namespace MegaBulkUploader.Modules.Clients
{
    public static class FileSystem
    {
        public record Settings(
            string Path,
            int SectionIndex,
            long SplitSize,
            int UploadStreams,
            string OutputFile,
            string BbOutputFile,
            bool OutputBbFile
        );

        public static long GetTotalFileSize(List<string> filePaths) => (from filePath in filePaths where File.Exists(filePath) select new FileInfo(filePath) into fi select fi.Length).Sum();

        //https://stackoverflow.com/a/32364847/23865160
        public static long GetDirectorySize(this DirectoryInfo? directoryInfo, bool recursive = true)
        {
            long startDirectorySize = 0;
            if (directoryInfo is not { Exists: true })
                return startDirectorySize;

            foreach (FileInfo fileInfo in directoryInfo.GetFiles())
                Interlocked.Add(ref startDirectorySize, fileInfo.Length);

            if (recursive)
                Parallel.ForEach(directoryInfo.GetDirectories(), (subDirectory) =>
                    Interlocked.Add(ref startDirectorySize, GetDirectorySize(subDirectory, recursive)));

            return startDirectorySize;
        }

        public static string FormatBytes(double bytes)
        {
            switch (bytes)
            {
                case < 0:
                case 0:
                    return "0B";
            }

            const double kb = 1024;
            const double mb = kb * 1024;
            const double gb = mb * 1024;
            const double tb = gb * 1024;

            return bytes switch
            {
                < kb => $"{bytes:F0}B",
                < mb => $"{Math.Round(bytes / kb, 2)}KB",
                < gb => $"{Math.Round(bytes / mb, 2)}MB",
                < tb => $"{Math.Round(bytes / gb, 2)}GB",
                _ => $"{Math.Round(bytes / tb, 2)}TB"
            };
        }

        public static List<List<string>> GetFolderStructure(string baseFolder, long maxSubFolderSize)
        {
            Log logger = new("Folder Manager");
            List<List<string>> allParts = [];
            string[] allFiles = Directory.GetFiles(baseFolder, "*", SearchOption.AllDirectories);
            long currentSize = 0;

            List<string> currentPart = [];
            allParts.Add(currentPart);

            foreach (string file in allFiles)
            {
                long fileSize = new FileInfo(file).Length;
                string relativePath = Path.GetRelativePath(baseFolder, file);

                if (currentSize + fileSize > maxSubFolderSize)
                {
                    logger.LogInformation($"Created section {allParts.Count}, with {currentPart.Count} files.");
                    currentPart = [];
                    allParts.Add(currentPart);
                    currentSize = 0;
                }

                string simulatedPath = Path.Combine(baseFolder, relativePath);
                currentPart.Add(simulatedPath);

                currentSize += fileSize;
            }
            logger.LogInformation($"Created section {allParts.Count}, with {currentPart.Count} files.");

            logger.LogInformation($"Total sections to upload: {allParts.Count}");

            return allParts;
        }

        public static async Task WriteBbLogAsync(string url, List<string> files, long totalSizeBytes, string bbLogName)
        {
            StringBuilder sb = new();

            sb.AppendLine($"{url.Replace(Environment.NewLine, "").Trim()} - {files.Count} Videos - {FormatBytes(totalSizeBytes)}\n");
            sb.AppendLine("[SPOILER=\"List\"]\n");

            foreach (string file in files) sb.AppendLine(Path.GetFileName(file));

            sb.AppendLine("\n[/SPOILER]\n");

            await File.AppendAllTextAsync(bbLogName, sb.ToString());
        }

        [SuppressMessage("ReSharper", "RedundantAssignment")] // Leave this
        public static async Task Process(Settings settings)
        {
            FileAttributes attributes = File.GetAttributes(settings.Path);
            List<List<string>> sections;

            if (attributes.HasFlag(FileAttributes.Directory))
                sections = GetFolderStructure(settings.Path, settings.SplitSize);
            else
            {
                if (new FileInfo(settings.Path).Length > settings.SplitSize)
                    throw new IOException("The file is too large to process. Maximum allowed size is 19 GB.");
                sections = [[settings.Path]];
            }

            string megaDirectory = Path.GetFileName(settings.Path.TrimEnd(Path.DirectorySeparatorChar));
            long totalDirectoryBytes = new DirectoryInfo(settings.Path).GetDirectorySize();
            int totalFiles = sections.Sum(x => x.Count);

            if (settings.OutputBbFile)
                await File.AppendAllTextAsync(settings.BbOutputFile, $"[SPOILER=\"{megaDirectory} - {FormatBytes(totalDirectoryBytes)} - {totalFiles} Videos - {sections.Count} Folders\"]\n");

            int index = 0;
            foreach (List<string> section in sections)
            {
                index++;
                if (settings.SectionIndex > index) continue;

                new Log("MegaUploader").LogInformation($"Starting section: {sections.IndexOf(section)+1}");

                Retry:
                (string email, string password, string token) = ("", "", "");

                try { (email, password, token) = await MailClient.CreateEmail(); }
                catch { new Log("EmailClient").LogError("Failed to create email, retrying..."); goto Retry; }

                if (new[] { email, password, token }.Any(string.IsNullOrEmpty))
                { new Log("EmailClient").LogError("Failed to create email 0x1, retrying..."); goto Retry; }

                MegaCliWrapper wrapper = new(token, section.Count);
                try { await wrapper.Register(email, password, Program.RandomString(5), megaDirectory); }
                catch { new Log("EmailClient").LogError("Failed to register account, retrying..."); goto Retry; }

                try { await wrapper.UploadFiles(section, megaDirectory, settings.UploadStreams); }
                catch (Exception ex) { new Log("MegaUploader").LogError($"An error occured while uploading files, please report to developer.{ex}"); continue; }

                try { await File.AppendAllTextAsync(settings.OutputFile, $"Login: {email} - {password}\nDirectory: {megaDirectory}\nTotal Size:{FormatBytes(GetTotalFileSize(section))}\nTotal Files:{section.Count}\nUrl: {Program.Exported.Last()}\n-{string.Join("\n- ", section.Select(Path.GetFileName))}\n\n"); }
                catch (Exception ex) { new Log("UploadLog").LogError($"An error occured while writing to log, please report to developer.{ex}"); }

                if (!settings.OutputBbFile) continue;

                try { await WriteBbLogAsync(Program.Exported.Last(), section, GetTotalFileSize(section), settings.BbOutputFile); }
                catch (Exception ex) { new Log("UploadLog").LogError($"An error occured while writing to BBlog, please report to developer.{ex}"); }
            }

            if (settings.OutputBbFile)
                await File.AppendAllTextAsync(settings.BbOutputFile, "\n[/SPOILER]");

            new Log("Completed").LogInformation($"Exported links: {string.Join(", ", Program.Exported)}");
            new Log("Completed").LogInformation("Press Ctrl+C to exit!");
            await MegaCliWrapper.DeleteCache();
        }
    }
}
