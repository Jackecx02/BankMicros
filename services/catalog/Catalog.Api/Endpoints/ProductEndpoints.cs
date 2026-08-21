using Catalog.Api.Data;
using Catalog.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return group;
    }

    private static async Task<Ok<List<Product>>> GetAll(CatalogDbContext db)
    {
        var products = await db.Products.AsNoTracking().ToListAsync();
        return TypedResults.Ok(products);
    }

    private static async Task<Results<Ok<Product>, NotFound>> GetById(Guid id, CatalogDbContext db)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<Created<Product>> Create(CreateProductRequest request, CatalogDbContext db)
    {
        var product = new Product(Guid.NewGuid(), request.Name, request.Description, request.Price, request.StockQuantity);

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/products/{product.Id}", product);
    }

    private static async Task<Results<Ok<Product>, NotFound>> Update(Guid id, CreateProductRequest request, CatalogDbContext db)
    {
        var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        var updated = existing with
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        db.Entry(existing).CurrentValues.SetValues(updated);
        await db.SaveChangesAsync();

        return TypedResults.Ok(updated);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(Guid id, CatalogDbContext db)
    {
        var existing = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        db.Products.Remove(existing);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
