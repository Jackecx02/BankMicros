using System.Text.Json;
using Basket.Api.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace Basket.Api.Data;

public class BasketRepository(IDistributedCache cache) : IBasketRepository
{

    public async Task<ShoppingCart?> GetCartAsync(Guid userId)
    {
        var result = await cache.GetStringAsync(GetKey(userId));
        return result is null ? null : JsonSerializer.Deserialize<ShoppingCart>(result);
    }

    public Task SaveCartAsync(ShoppingCart cart)
    {
       var json = JsonSerializer.Serialize(cart);
       return cache.SetStringAsync(GetKey(cart.UserId), json);
    }

    public Task DeleteCartAsync(Guid userId) => cache.RemoveAsync(GetKey(userId));
    private static string GetKey(Guid userId) => $"basket:{userId}";
}