using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using XmlMiddleware.Application.Interfaces.Services;

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

    public async Task<Stream> ReadAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var blobClient =
            _containerClient.GetBlobClient(blobPath);

        var response =
            await blobClient.DownloadStreamingAsync(
                cancellationToken: cancellationToken);

        var memoryStream = new MemoryStream();

        await response.Value.Content.CopyToAsync(
            memoryStream,
            cancellationToken);

        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task<string> UploadAsync(
        Stream stream,
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var blobClient =
            _containerClient.GetBlobClient(blobPath);

        stream.Position = 0;

        await blobClient.UploadAsync(
            stream,
            overwrite: true,
            cancellationToken: cancellationToken);

        return blobClient.Uri.ToString();
    }

    public async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var sourceBlob = _containerClient.GetBlobClient(sourcePath);
        var destinationBlob = _containerClient.GetBlobClient(destinationPath);

        var copyOperation = await destinationBlob.StartCopyFromUriAsync(
            sourceBlob.Uri,
            cancellationToken: cancellationToken);

        await copyOperation.WaitForCompletionAsync(cancellationToken);
    }

    public async Task DeleteIfExistsAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        await _containerClient.GetBlobClient(blobPath).DeleteIfExistsAsync(
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var paths = new List<string>();

        await foreach (var blob in _containerClient.GetBlobsAsync(
                   traits: Azure.Storage.Blobs.Models.BlobTraits.None,
                   states: Azure.Storage.Blobs.Models.BlobStates.None,
                   prefix: prefix,
                   cancellationToken: cancellationToken))
        {
            paths.Add(blob.Name);
        }

        return paths;
    }

    public async Task<bool> ExistsAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var blobClient =
            _containerClient.GetBlobClient(blobPath);

        var result =
            await blobClient.ExistsAsync(cancellationToken);

        return result.Value;
    }

    public async Task MoveAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var sourceBlob =
            _containerClient.GetBlobClient(sourcePath);

        var destinationBlob =
            _containerClient.GetBlobClient(destinationPath);

        var copyOperation =
            await destinationBlob.StartCopyFromUriAsync(
                sourceBlob.Uri,
                cancellationToken: cancellationToken);

        await copyOperation.WaitForCompletionAsync(
            cancellationToken);

        await sourceBlob.DeleteIfExistsAsync(
            cancellationToken: cancellationToken);
    }

    public async Task<bool> ContainerExistsAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            await _containerClient.ExistsAsync(
                cancellationToken);

        return result.Value;
    }
}