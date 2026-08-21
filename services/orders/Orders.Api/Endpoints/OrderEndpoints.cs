using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Models;
using Orders.Api.Services;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Shared.Contracts.Events;
using Shared.Contracts.Messaging;

namespace Orders.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/user/{userId:guid}", GetByUserId);
        group.MapPost("/", Checkout);

        return group;
    }

    private static async Task<Results<Ok<Order>, NotFound>> GetById(Guid id, OrdersDbContext db)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }

    private static async Task<Ok<List<Order>>> GetByUserId(Guid userId, OrdersDbContext db)
    {
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .ToListAsync();

        return TypedResults.Ok(orders);
    }

    private static async Task<Results<Created<Order>, BadRequest<string>, ProblemHttpResult>> Checkout(
        CreateOrderRequest request,
        IBasketServiceClient basketClient,
        IEventPublisher eventPublisher,
        OrdersDbContext db)
    {
        BasketDto? basket;
        try
        {
            basket = await basketClient.GetBasketAsync(request.UserId);
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException)
        {
            return TypedResults.Problem(
                "El carrito no está disponible en este momento. Intenta de nuevo en unos segundos.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (basket is null || basket.Items.Count == 0)
        {
            return TypedResults.BadRequest("El carrito está vacío o no existe.");
        }

        var items = basket.Items
            .Select(i => new OrderItem(i.ProductId, i.ProductName, i.Price, i.Quantity))
            .ToList();

        var order = new Order(
            Guid.NewGuid(),
            request.UserId,
            items.Sum(i => i.Price * i.Quantity),
            DateTimeOffset.UtcNow,
            OrderStatus.Pending)
        {
            Items = items
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            order.UserId,
            order.CreatedAt,
            items.Select(i => new OrderItemEvent(i.ProductId, i.ProductName, i.Price, i.Quantity)).ToList());

        await eventPublisher.PublishAsync(KafkaTopics.OrderCreated, order.UserId.ToString(), orderCreatedEvent);

        return TypedResults.Created($"/orders/{order.Id}", order);
    }
}
