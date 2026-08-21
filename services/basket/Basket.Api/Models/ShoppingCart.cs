namespace Basket.Api.Models;

public record ShoppingCart(Guid UserId, List<CartItem> Items);

public record CartItem(Guid ProductId, string ProductName, decimal Price, int Quantity);

public record AddItemRequest(Guid ProductId, int Quantity);