using CG.Web.MegaApiClient;
using MegaBulkUploader.Modules.Output;
using System.Diagnostics.CodeAnalysis;

namespace MegaBulkUploader.Modules.Clients
{
	public class MegaClientWrapper
	{
		private static readonly MegaApiClient MegaApiClient = new();
		private static MegaCliWrapper _megaCliWrapper = null!;

		public MegaClientWrapper(MegaCliWrapper cli)
		{
			Log log = new("MegaClient");
			_megaCliWrapper = cli;
			MegaApiClient.ApiRequestFailed += (_, failed) => log.LogError(failed.ResponseJson);
		}

		public async Task Login(string username, string password)
		{
			if (MegaApiClient.IsLoggedIn)
				await MegaApiClient.LogoutAsync();

			await _megaCliWrapper.Login(username, password);
			await MegaApiClient.LoginAsync(username, password);
		}

		public async Task<INode> GetBaseNode(string nodeName)
		{
			IEnumerable<INode>? nodes = await MegaApiClient.GetNodesAsync();
			IEnumerable<INode> enumerable = nodes as INode[] ?? nodes.ToArray();
			INode root = enumerable.Single(x => x.Type == NodeType.Root);
			INode? myFolder = enumerable.FirstOrDefault(x => x.Type == NodeType.Directory && x.Name == nodeName);

			if (myFolder is not null) return myFolder;

			await MegaApiClient.CreateFolderAsync(nodeName, root);
			return await GetBaseNode(nodeName);
		}

		[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
		[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
		public async Task UploadFiles(string nodeName, IReadOnlyList<string> files)
		{
			INode myFolder = await GetBaseNode(nodeName);

			int uploadedCount = 0;

			foreach (string filePath in files)
			{
				if (!File.Exists(filePath)) continue;

				FileInfo fileInfo = new (filePath);

				MegaCliWrapper.TransferData transfer = new(
					Source: filePath,
					Destination: myFolder.Name,
					Progress: 0.0,
					Status: "Uploading",
					Size: FileSystem.FormatBytes(fileInfo.Length)
				);

				ProgressBar progressBar = new ();

				IProgress<double> progress = new Progress<double>(progressValue =>
				{
					MegaCliWrapper.TransferData updatedTransfer = transfer with { Progress = Math.Round(progressValue, 2, MidpointRounding.ToEven) };
					ValueTuple<MegaCliWrapper.TransferData, int, int> payload = new(updatedTransfer, uploadedCount, files.Count);

					progressBar.Report(payload);
				});

				await MegaApiClient.UploadFileAsync(filePath, myFolder, progress);
				progressBar.Dispose();
				uploadedCount++;
			}

			INode node = await GetBaseNode(nodeName);
			
			await _megaCliWrapper.ExecuteCli("export", "Exported", "-a", "-f", $"\"/{node.Name}");
		}

		public async Task UploadFile(string filePath) => await UploadFiles(Path.GetFileNameWithoutExtension(filePath), [filePath]);
	}
}
