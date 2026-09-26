using XmlMiddleware.Application.Models;
using XmlMiddleware.Domain.Enums;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IFileGeneratorService
{
    Task<MemoryStream> GenerateAsync(
        List<CanonicalOrderModel> orders,
        OutputType outputType,
        CancellationToken cancellationToken = default);
}