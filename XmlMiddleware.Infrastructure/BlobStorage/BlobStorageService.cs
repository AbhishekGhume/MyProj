using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using XmlMiddleware.Application.Interfaces.Services;
using Azure.Storage.Blobs.Models;

namespace XmlMiddleware.Infrastructure.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString =
            configuration["AzureWebJobsStorage"];

        var containerName =
            configuration["ContainerName"];

        _containerClient =
            new BlobContainerClient(
                connectionString,
                containerName);
    }

    public async Task<Stream> ReadAsync(string blobPath)
    {
        var blobClient =
            _containerClient.GetBlobClient(blobPath);

        var response =
            await blobClient.DownloadStreamingAsync();

        var memoryStream = new MemoryStream();

        await response.Value.Content.CopyToAsync(memoryStream);

        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string blobPath)
    {
        var blobClient =
            _containerClient.GetBlobClient(blobPath);

        stream.Position = 0;

        await blobClient.UploadAsync(
            stream,
            overwrite: true);

        return blobClient.Uri.ToString();
    }

    public async Task<bool> ExistsAsync(string blobPath)
    {
        var blobClient =
            _containerClient.GetBlobClient(blobPath);

        return await blobClient.ExistsAsync();
    }

    public async Task MoveAsync(
    string sourcePath,
    string destinationPath)
    {
        var sourceBlob =
            _containerClient.GetBlobClient(sourcePath);

        var destinationBlob =
            _containerClient.GetBlobClient(destinationPath);

        var copyOperation =
            await destinationBlob.StartCopyFromUriAsync(
                sourceBlob.Uri);

        await copyOperation.WaitForCompletionAsync();

        await sourceBlob.DeleteIfExistsAsync();
    }

    public async Task<bool> ContainerExistsAsync()
    {
        var result =
            await _containerClient.ExistsAsync();

        return result.Value;
    }
}