using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Shared.Contracts.Messaging;

public class KafkaEventPublisher(IProducer<string, string> producer, ILogger<KafkaEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(string topic, string key, TEvent @event)
    {
        var payload = JsonSerializer.Serialize(@event);

        var result = await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = payload
        });

        logger.LogInformation(
            "Published {EventType} to {Topic} [partition {Partition}, offset {Offset}]",
            typeof(TEvent).Name, topic, result.Partition.Value, result.Offset.Value);
    }
}
