using System.Text.Json;
using Catalog.Api.Data;
using Catalog.Api.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events;
using Shared.Contracts.Messaging;

namespace Catalog.Api.Messaging;

public class OrderCreatedConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IEventPublisher eventPublisher,
    ILogger<OrderCreatedConsumer> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => Run(stoppingToken), stoppingToken);

    private void Run(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "catalog-api",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.OrderCreated);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    var orderCreated = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);

                    if (orderCreated is null)
                    {
                        continue;
                    }

                    ReserveStock(orderCreated, stoppingToken).GetAwaiter().GetResult();
                }
                catch (ConsumeException ex)
                {
                    // Errores transitorios (el tópico todavía no existe, el broker se está reiniciando, etc.)
                    // no deben tumbar el servicio entero: los reintentamos con una espera corta.
                    logger.LogWarning(ex, "Error transitorio consumiendo de Kafka, reintentando en 2s");
                    Thread.Sleep(2000);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown normal
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ReserveStock(OrderCreatedEvent orderCreated, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Paso 1: verificar TODOS los items antes de tocar la base de datos.
        // Si alguno no alcanza, no reservamos nada (todo o nada) y publicamos el fallo.
        var products = new Dictionary<Guid, Product>();
        foreach (var item in orderCreated.Items)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);

            if (product is null || product.StockQuantity < item.Quantity)
            {
                var reason = product is null
                    ? $"El producto {item.ProductId} ya no existe en el catálogo."
                    : $"Stock insuficiente para '{product.Name}': quedan {product.StockQuantity}, se pidieron {item.Quantity}.";

                logger.LogWarning("Reserva de stock rechazada para la orden {OrderId}: {Reason}", orderCreated.OrderId, reason);

                await eventPublisher.PublishAsync(
                    KafkaTopics.StockReservationFailed,
                    orderCreated.OrderId.ToString(),
                    new StockReservationFailedEvent(orderCreated.OrderId, orderCreated.UserId, reason));

                return;
            }

            products[item.ProductId] = product;
        }

        // Paso 2: todos los items tienen stock suficiente, ahora sí decrementamos.
        foreach (var item in orderCreated.Items)
        {
            var product = products[item.ProductId];
            db.Entry(product).CurrentValues.SetValues(product with { StockQuantity = product.StockQuantity - item.Quantity });
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stock reservado para la orden {OrderId}", orderCreated.OrderId);

        await eventPublisher.PublishAsync(
            KafkaTopics.StockReserved,
            orderCreated.OrderId.ToString(),
            new StockReservedEvent(orderCreated.OrderId, orderCreated.UserId));
    }
}
