namespace XmlMiddleware.Application.Interfaces.Services;

public interface IBlobStorageService
{
    Task<Stream> ReadAsync(
        string blobPath,
        CancellationToken cancellationToken = default);

    Task<string> UploadAsync(
        Stream stream,
        string blobPath,
        CancellationToken cancellationToken = default);

    Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string blobPath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string blobPath,
        CancellationToken cancellationToken = default);

    Task<bool> ContainerExistsAsync(
        CancellationToken cancellationToken = default);
}