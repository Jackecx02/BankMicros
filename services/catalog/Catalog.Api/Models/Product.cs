namespace Catalog.Api.Models;

public record Product(Guid Id, string Name, string Description, decimal Price, int StockQuantity);

public record CreateProductRequest(string Name, string Description, decimal Price, int StockQuantity);
