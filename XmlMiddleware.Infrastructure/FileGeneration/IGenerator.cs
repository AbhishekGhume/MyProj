using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public interface IGenerator
{
    MemoryStream Generate(
        List<CanonicalOrderModel> orders);
}