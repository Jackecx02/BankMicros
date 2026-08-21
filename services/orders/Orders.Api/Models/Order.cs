namespace Orders.Api.Models;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled
}

public record Order(Guid Id, Guid UserId, decimal TotalAmount, DateTimeOffset CreatedAt, OrderStatus Status)
{
    public List<OrderItem> Items { get; init; } = [];
}

public record OrderItem(Guid ProductId, string ProductName, decimal Price, int Quantity);

public record CreateOrderRequest(Guid UserId);
