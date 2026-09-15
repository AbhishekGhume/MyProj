namespace XmlMiddleware.Application.Interfaces.Services;

public interface IHashService
{
    Task<string> GenerateHashAsync(Stream stream);
}