namespace Basket.Api.Services;

public interface ICatalogServiceClient
{
    Task<CatalogProductDto?> GetProductAsync(Guid productId);
}
