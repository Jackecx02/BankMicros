using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Models;
using Shared.Contracts.Events;

namespace Orders.Api.Messaging;

public class StockReservedConsumer(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<StockReservedConsumer> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => Run(stoppingToken), stoppingToken);

    private void Run(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "orders-api-stock-reserved",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.StockReserved);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    var stockReserved = JsonSerializer.Deserialize<StockReservedEvent>(result.Message.Value);

                    if (stockReserved is null)
                    {
                        continue;
                    }

                    UpdateStatus(stockReserved.OrderId, OrderStatus.Confirmed, stoppingToken).GetAwaiter().GetResult();
                    logger.LogInformation("Orden {OrderId} confirmada", stockReserved.OrderId);
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

    private async Task UpdateStatus(Guid orderId, OrderStatus status, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        db.Entry(order).CurrentValues.SetValues(order with { Status = status });
        await db.SaveChangesAsync(cancellationToken);
    }
}
