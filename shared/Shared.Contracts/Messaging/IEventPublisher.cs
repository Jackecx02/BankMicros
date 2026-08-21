namespace Shared.Contracts.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(string topic, string key, TEvent @event);
}
