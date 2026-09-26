using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Application.Interfaces.Services;

public interface IXmlMapperService
{
    Task<List<CanonicalOrderModel>> MapAsync(
        Stream xmlStream,
        CancellationToken cancellationToken = default);
}