using System.Text.Json.Serialization;

namespace XmlMiddleware.Application.Messages;

public class EventGridBlobData
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}