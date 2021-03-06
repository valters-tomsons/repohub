using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;

namespace hubservice.Providers;

public class AzureStorageProvider
{
	private const string ContainerName = "$web";

	private readonly CloudStorageAccount storageAccount;
	private CloudBlobClient? client;
	private CloudBlobContainer? container;

	public AzureStorageProvider(IConfiguration config)
	{
		var connectionString = config.GetSection("ConnectionStrings").GetValue<string>("RepositoryStorage");
		storageAccount = CloudStorageAccount.Parse(connectionString);
	}

	public async Task<DateTimeOffset?> GetLastModified(string fileName)
	{
		var blob = container?.GetBlockBlobReference(fileName);

		if (blob is null)
		{
			return null;
		}

		var exists = await blob.ExistsAsync();

		if (!exists)
		{
			return null;
		}

		await blob.FetchAttributesAsync().ConfigureAwait(false);
		return blob.Properties.LastModified;
	}

	public async Task<byte[]?> ReadBytesFromStorage(string fileName)
	{
		var blob = container?.GetBlockBlobReference(fileName);

		if (blob is null)
        {
            return null;
        }

		var exists = await blob.ExistsAsync().ConfigureAwait(false);

		if (!exists)
		{
			return null;
		}

		await blob.FetchAttributesAsync().ConfigureAwait(false);

		byte[] imageBuffer = new byte[blob.Properties.Length];
		await blob.DownloadToByteArrayAsync(imageBuffer, 0).ConfigureAwait(false);

		return imageBuffer;
	}

	public async Task DownloadFileToDisk(string storagePath, string localPath)
	{
		var file = await ReadBytesFromStorage(storagePath).ConfigureAwait(false);

		if (file != null)
		{
			await File.WriteAllBytesAsync(localPath, file).ConfigureAwait(false);
		}
	}

	public async Task<bool> WriteDataToStorage(string fileName, byte[] buffer)
	{
		var blob = container?.GetBlockBlobReference(fileName);

		if (blob is null)
        {
            return false;
        }

		blob.Properties.ContentType = "application/octet-stream";
		await blob.UploadFromByteArrayAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

        return true;
	}

	public async Task<bool> WriteFileToStorage(string fileName, string localPath)
	{
		var blob = container?.GetBlockBlobReference(fileName);

		if (blob is null)
        {
            return false;
        }

		blob.Properties.ContentType = "application/octet-stream";
		await blob.UploadFromFileAsync(localPath).ConfigureAwait(false);

        return true;
	}

	public async Task<bool> FileExists(string fileName)
	{
		var blob = container?.GetBlockBlobReference(fileName);

        if(blob is null)
        {
            return false;
        }

		return await blob.ExistsAsync();
	}

	public async Task InitializeStorage()
	{
		if (client is not null)
		{
			return;
		}

		client = storageAccount.CreateCloudBlobClient();
		container = client.GetContainerReference(ContainerName);
		await container.CreateIfNotExistsAsync().ConfigureAwait(false);
	}
}