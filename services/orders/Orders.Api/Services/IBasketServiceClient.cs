namespace Orders.Api.Services;

public interface IBasketServiceClient
{
    Task<BasketDto?> GetBasketAsync(Guid userId);
}
