namespace Basket.Api.Services;

public record CatalogProductDto(Guid Id, string Name, decimal Price, int StockQuantity);
