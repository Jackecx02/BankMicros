namespace Shared.Contracts.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    Guid UserId,
    DateTimeOffset CreatedAt,
    List<OrderItemEvent> Items);

public record OrderItemEvent(Guid ProductId, string ProductName, decimal Price, int Quantity);
