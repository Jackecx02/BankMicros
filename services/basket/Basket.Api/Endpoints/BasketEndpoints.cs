using Basket.Api.Data;
using Basket.Api.Models;
using Basket.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Basket.Api.Endpoints;

public static class BasketEndpoints
{
    public static RouteGroupBuilder MapBasketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/basket").WithTags("Basket");

        group.MapGet("/{userId:guid}", GetByUserId);
        group.MapPost("/{userId:guid}/items", AddItem);
        group.MapDelete("/{userId:guid}/items/{productId:guid}", DeleteItemFromBasket);
        group.MapDelete("/{userId:guid}", Delete);

        return group;
    }

    private static async Task<Results<Ok<ShoppingCart>, NotFound>> GetByUserId(Guid userId, IBasketRepository repository)
    {
        var cart = await repository.GetCartAsync(userId);
        return cart is null ? TypedResults.NotFound() : TypedResults.Ok(cart);
    }

    private static async Task<Results<Ok<ShoppingCart>, NotFound, ProblemHttpResult>> AddItem(
        Guid userId, AddItemRequest request, IBasketRepository repository, ICatalogServiceClient catalogClient)
    {
        CatalogProductDto? product;
        try
        {
            product = await catalogClient.GetProductAsync(request.ProductId);
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException)
        {
            return TypedResults.Problem(
                "El catálogo no está disponible en este momento. Intenta de nuevo en unos segundos.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (product is null)
        {
            return TypedResults.NotFound();
        }

        var cart = await repository.GetCartAsync(userId) ?? new ShoppingCart(userId, []);

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existing is not null)
        {
            cart.Items.Remove(existing);
            cart.Items.Add(existing with { Quantity = existing.Quantity + request.Quantity });
        }
        else
        {
            cart.Items.Add(new CartItem(product.Id, product.Name, product.Price, request.Quantity));
        }

        await repository.SaveCartAsync(cart);
        return TypedResults.Ok(cart);
    }

    private static async Task<Results<Ok<ShoppingCart>, NotFound>> DeleteItemFromBasket(Guid userId, Guid productId, IBasketRepository repository)
    {
        var cart = await repository.GetCartAsync(userId);
        if (cart is null)
        {
            return TypedResults.NotFound();
        }

        cart.Items.RemoveAll(i => i.ProductId == productId);
        await repository.SaveCartAsync(cart);
        return TypedResults.Ok(cart);
    }

    private static async Task<NoContent> Delete(Guid userId, IBasketRepository repository)
    {
        await repository.DeleteCartAsync(userId);
        return TypedResults.NoContent();
    }
}
