namespace XmlMiddleware.Application.Interfaces.Services;

public interface IBlobStorageService
{
    Task<Stream> ReadAsync(string blobPath);

    Task<string> UploadAsync(
    Stream stream,
    string blobPath);

    Task MoveAsync(
    string sourcePath,
    string destinationPath);

    Task<bool> ExistsAsync(string blobPath);

    Task<bool> ContainerExistsAsync();
}