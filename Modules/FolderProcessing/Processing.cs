using MegaBulkUploader.Modules.Clients;
using MegaBulkUploader.Modules.Output;
using System.Diagnostics;
using System.IO;
using static System.Net.WebRequestMethods;

namespace MegaBulkUploader.Modules.FolderProcessing
{
    public class Processing
    {
        
        public async Task Process(string directoryToUpload)
        {
            List<string> exportedLinks = [];

            List<List<string>> sections = SubFolderCreator.GetFolderStructure(directoryToUpload, 19327352832);
            
            foreach (List<string> section in sections)
            {
                try
                {
                    (string email, string password, string token) = await MailClient.CreateEmail();
                    MegaCliWrapper wrapper = new(token, section.Count);
                    await wrapper.Register(email, password, Program.RandomString(5), Path.GetFileName(directoryToUpload.TrimEnd(Path.DirectorySeparatorChar)));
                    await wrapper.UploadFiles(section, Path.GetFileName(directoryToUpload.TrimEnd(Path.DirectorySeparatorChar)));
                    exportedLinks.AddRange(wrapper.ExportedLinks);
                }
                catch
                {
                    new Log("MailClient").LogError("Failed to create email, please retry.");
                    break;
                }
            }

            Log logger = new("Init");
            logger.LogInformation($"Exported links: {string.Join(", ", exportedLinks)}");
        }
    }
}
