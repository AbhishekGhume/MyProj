namespace XmlMiddleware.Application.Interfaces.Services;

using XmlMiddleware.Application.Models;

public interface IXmlValidationService
{
    Task<XmlValidationResult> ValidateAsync(Stream stream);
}