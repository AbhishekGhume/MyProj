using System.Text.Json.Serialization;

namespace XmlMiddleware.Application.Messages;

public class EventGridBlobCreatedMessage
{
    /// <summary>Event Grid event id. Stable across redeliveries, used to derive the CorrelationId.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public EventGridBlobData Data { get; set; } = new();
}