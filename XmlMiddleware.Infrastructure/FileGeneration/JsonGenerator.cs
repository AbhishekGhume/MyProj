using System.Text;
using System.Text.Json;
using XmlMiddleware.Application.Models;

namespace XmlMiddleware.Infrastructure.FileGeneration;

public class JsonGenerator : IGenerator
{
    public MemoryStream Generate(
        List<CanonicalOrderModel> orders)
    {
        var json = JsonSerializer.Serialize(
            orders,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        return new MemoryStream(
            Encoding.UTF8.GetBytes(json));
    }
}