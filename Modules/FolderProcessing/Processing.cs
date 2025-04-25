using MegaBulkUploader.Modules.Clients;
using MegaBulkUploader.Modules.Output;

namespace MegaBulkUploader.Modules.FolderProcessing
{
    public static class Processing
    {
        public static async Task Process(string objectToProcess)
        {
            FileAttributes attributes = File.GetAttributes(objectToProcess);
            List<List<string>> sections;

            if (attributes.HasFlag(FileAttributes.Directory))
                sections = SubFolderCreator.GetFolderStructure(objectToProcess, 19327352832);
            else
            {
                if (new FileInfo(objectToProcess).Length > 19327352832)
                    throw new IOException("The file is too large to process. Maximum allowed size is 19 GB.");
                sections = [[objectToProcess]];
            }

            foreach (List<string> section in sections)
            {
                (string email, string password, string token) = ("", "", "");

                try { (email, password, token) = await MailClient.CreateEmail(); }
                catch { new Log("EmailClient").LogError("Failed to create email, please retry."); continue; }

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(token)) 
                { new Log("MegaRegister").LogError("Failed to create email 0x1, please retry."); continue; }

                MegaCliWrapper wrapper = new(token, section.Count);
                try { await wrapper.Register(email, password, Program.RandomString(5), Path.GetFileName(objectToProcess.TrimEnd(Path.DirectorySeparatorChar))); }
                catch { new Log("MegaRegister").LogError("Failed to register account, please retry."); continue; }

                try { await wrapper.UploadFiles(section, Path.GetFileName(objectToProcess.TrimEnd(Path.DirectorySeparatorChar))); }
                catch (Exception ex) { new Log("MegaUploader").LogError($"An error occured while uploading files, please report to developer.{ex}"); continue; }

                try { await File.AppendAllTextAsync("Upload.log", $"Url: {Program.Exported.Last()}{string.Join("\n- ", Program.Exported)}"); }
                catch (Exception ex) { new Log("UploadLog").LogError($"An error occured while writing to log, please report to developer.{ex}"); }
            }

            new Log("Completed").LogInformation($"Exported links: {string.Join(", ", Program.Exported)}");
        }
    }
}
