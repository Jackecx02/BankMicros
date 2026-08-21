using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Models;
using Shared.Contracts.Events;

namespace Orders.Api.Messaging;

public class StockReservationFailedConsumer(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<StockReservationFailedConsumer> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => Run(stoppingToken), stoppingToken);

    private void Run(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "orders-api-stock-reservation-failed",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.StockReservationFailed);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    var stockFailed = JsonSerializer.Deserialize<StockReservationFailedEvent>(result.Message.Value);

                    if (stockFailed is null)
                    {
                        continue;
                    }

                    UpdateStatus(stockFailed.OrderId, stockFailed.Reason, stoppingToken).GetAwaiter().GetResult();
                    logger.LogWarning("Orden {OrderId} cancelada: {Reason}", stockFailed.OrderId, stockFailed.Reason);
                }
                catch (ConsumeException ex)
                {
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

    private async Task UpdateStatus(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        db.Entry(order).CurrentValues.SetValues(order with { Status = OrderStatus.Cancelled });
        await db.SaveChangesAsync(cancellationToken);
    }
}
