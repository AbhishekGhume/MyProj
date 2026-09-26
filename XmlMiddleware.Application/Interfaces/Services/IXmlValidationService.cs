using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IXmlValidationService
{
    Task<XmlValidationResult> ValidateAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}