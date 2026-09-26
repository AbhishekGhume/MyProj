using System.Security.Cryptography;
using XmlMiddleware.Application.Interfaces.Services;

namespace XmlMiddleware.Infrastructure.Hashing;

public class HashService : IHashService
{
    public async Task<string> GenerateHashAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();

        var hashBytes = await sha256.ComputeHashAsync(
            stream,
            cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hashBytes);
    }
}