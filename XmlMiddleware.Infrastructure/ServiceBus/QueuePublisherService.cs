using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using XmlMiddleware.Application.Interfaces.Services;
using XmlMiddleware.Application.Messages;

namespace XmlMiddleware.Infrastructure.ServiceBus;

public class QueuePublisherService
    : IMessagePublisherService
{
    private readonly ServiceBusClient _client;
    private readonly string _queueName;

    public QueuePublisherService(
        IConfiguration configuration)
    {
        _client = new ServiceBusClient(
            configuration["ServiceBusConnectionString"]);

        _queueName =
            configuration["QueueName"]!;
    }

    public async Task PublishAsync(
        FileProcessingMessage message)
    {
        var sender =
            _client.CreateSender(_queueName);

        var payload =
            JsonSerializer.Serialize(message);

        var serviceBusMessage =
            new ServiceBusMessage(payload)
            {
                MessageId = Guid.NewGuid().ToString()
            };

        await sender.SendMessageAsync(
            serviceBusMessage);
    }
}