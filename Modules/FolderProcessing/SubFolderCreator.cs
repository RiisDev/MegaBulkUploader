using MegaBulkUploader.Modules.Output;

namespace MegaBulkUploader.Modules.FolderProcessing
{
    public static class SubFolderCreator
    {
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

        public static List<string> CreateSubFolders(string baseFolder, long maxSubFolderSize)
        {
            Log logger = new("Folder Manager");
            List<string> folderList = [];
            string directoryName = new DirectoryInfo(baseFolder).Name;
            string[] allFiles = Directory.GetFiles(baseFolder, "*", SearchOption.AllDirectories);
            string baseSubFolder = $"{directoryName}-Part-";
            int index = 1;

            string subFolder = Path.Combine(baseFolder, baseSubFolder + index);
            Directory.CreateDirectory(subFolder);
            logger.LogInformation($"Created folder {subFolder}");
            folderList.Add(subFolder);

            long currentSize = 0;

            foreach (string file in allFiles)
            {
                string relativePath = Path.GetRelativePath(baseFolder, file);
                string targetFilePath = Path.Combine(subFolder, relativePath);
                string targetDirectory = Path.GetDirectoryName(targetFilePath)!;

                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                long fileSize = new FileInfo(file).Length;

                if (currentSize + fileSize > maxSubFolderSize)
                {
                    index++;
                    subFolder = Path.Combine(baseFolder, baseSubFolder + index);
                    Directory.CreateDirectory(subFolder);
                    logger.LogInformation($"Created folder {subFolder}");
                    folderList.Add(subFolder);
                    currentSize = 0;

                    targetFilePath = Path.Combine(subFolder, relativePath);
                    targetDirectory = Path.GetDirectoryName(targetFilePath)!;

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }
                }

                File.Move(file, targetFilePath);
                currentSize += fileSize;
            }
            return folderList;
        }

    }
}
