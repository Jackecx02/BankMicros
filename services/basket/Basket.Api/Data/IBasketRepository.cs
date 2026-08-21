using Basket.Api.Models;

namespace Basket.Api.Data;

public interface IBasketRepository
{
    Task<ShoppingCart?> GetCartAsync(Guid userId);
    Task SaveCartAsync(ShoppingCart cart);
    Task DeleteCartAsync(Guid userId);
}