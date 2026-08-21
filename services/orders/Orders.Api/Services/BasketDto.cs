namespace Orders.Api.Services;

public record BasketDto(Guid UserId, List<BasketItemDto> Items);

public record BasketItemDto(Guid ProductId, string ProductName, decimal Price, int Quantity);
