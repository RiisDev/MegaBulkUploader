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
            bool OutputBbFile, 
            bool UseOldUpload
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
            Log logger = new("MegaUploader");

            logger.LogInformation("Using settings:");
            logger.LogInformation($"- Path: '{settings.Path}'");
            logger.LogInformation($"- SectionIndex: {settings.SectionIndex}");
            logger.LogInformation($"- SplitSize: {settings.SplitSize} | {FormatBytes(settings.SplitSize)}");
            logger.LogInformation($"- UploadStreams: {settings.UploadStreams}");
            logger.LogInformation($"- OutputFile: '{settings.OutputFile}'");
            logger.LogInformation($"- BbOutputFile: '{settings.BbOutputFile}'");
            logger.LogInformation($"- OutputBbFile: {settings.OutputBbFile}");


            FileAttributes attributes = File.GetAttributes(settings.Path);
            List<List<string>> sections = [];

            if (settings.Path.Contains('|'))
            {
                string[] files = settings.Path.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
				long currentSectionSize = 0;
				List<string> currentSection = [];
				sections.Add(currentSection);

				foreach (string file in files)
				{
					if (!File.Exists(file))
						throw new FileNotFoundException(
							$"The specified file '{file}' does not exist.");

					FileInfo fileInfo = new(file);

					if (fileInfo.Length > settings.SplitSize)
						throw new IOException(
							$"The file '{file}' exceeds the maximum allowed size of {settings.SplitSize:N0} bytes.");

					if (currentSectionSize + fileInfo.Length > settings.SplitSize)
					{
						currentSection = [];
						sections.Add(currentSection);
						currentSectionSize = 0;
					}

					currentSection.Add(file);
					currentSectionSize += fileInfo.Length;
				}
			}
            else if (attributes.HasFlag(FileAttributes.Directory))
                sections = GetFolderStructure(settings.Path, settings.SplitSize);
            else
            {
                if (new FileInfo(settings.Path).Length > settings.SplitSize)
	                throw new IOException("The file is too large to process. Maximum allowed size is 19 GB.");
                sections = [[settings.Path]];
            }

            string megaDirectory = Path.GetFileName(settings.Path.TrimEnd(Path.DirectorySeparatorChar));

            long totalDirectoryBytes = attributes.HasFlag(FileAttributes.Directory) ? new DirectoryInfo(settings.Path).GetDirectorySize() : new FileInfo(settings.Path).Length;

            int totalFiles = sections.Sum(x => x.Count);

            if (settings.OutputBbFile)
                await File.AppendAllTextAsync(settings.BbOutputFile, $"[SPOILER=\"{megaDirectory} - {FormatBytes(totalDirectoryBytes)} - {totalFiles} Videos - {sections.Count} Folders\"]\n");

			int index = 0;
			foreach (List<string> section in sections)
			{
				index++;
				if (settings.SectionIndex > index) continue;

				logger.LogInformation($"Starting section: {sections.IndexOf(section) + 1}");

				bool success = false;
				string logState = "";
				while (!success)
				{
					(string email, string password, string token) = ("", "", "");

					MegaCliWrapper? wrapper = null;
					MegaClientWrapper? client = null;

					try
					{
						logState = "EmailClient";
						(email, password, token) = await MailClient.CreateEmail();
						if (new[] { email, password, token }.Any(string.IsNullOrEmpty)) throw new InvalidOperationException("Email creation returned empty values.");

						wrapper = new MegaCliWrapper(token, section.Count);
						client = new MegaClientWrapper(wrapper);

						client.OnMegaThreeError += () => throw new MegaThreeException();

						await wrapper.Register(email, password, Program.RandomString(5), megaDirectory);

						if (settings.UseOldUpload)
						{
							logState = "MegaCliDevException";
							await wrapper.UploadFiles(section, megaDirectory, settings.UploadStreams);
						}
						else
						{
							logState = "MegaClientWrapperDevException";
							MegaCliWrapper.KillMegaProcesses();
							await MegaCliWrapper.DeleteCache();

							await client.Login(email, password);
							await client.UploadFiles(megaDirectory, section);
						}

						success = true;
					}
					catch (MegaThreeException) { logger.LogWarning("MegaErrorThree occurred, retrying section..."); client?.Dispose(); wrapper?.Dispose(); }
					catch (Exception ex) { new Log(logState).LogError($"Unexpected error, retrying... {ex}"); }
					finally { wrapper?.Dispose(); client?.Dispose(); }
				}

				if (!settings.OutputBbFile) continue;

				try { await WriteBbLogAsync(Program.Exported.Last(), section, GetTotalFileSize(section), settings.BbOutputFile); }
				catch (Exception ex) { new Log("UploadLog").LogError($"Failed writing BBlog.{ex}"); }
			}


			if (settings.OutputBbFile)
                await File.AppendAllTextAsync(settings.BbOutputFile, "\n[/SPOILER]");

            new Log("Completed").LogInformation($"Exported links: {string.Join(", ", Program.Exported)}");
            new Log("Completed").LogInformation("Press Ctrl+C to exit!");
            await MegaCliWrapper.DeleteCache();
        }
    }
}
