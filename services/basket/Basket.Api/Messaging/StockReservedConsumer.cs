using System.Text.Json;
using Basket.Api.Data;
using Confluent.Kafka;
using Shared.Contracts.Events;

namespace Basket.Api.Messaging;

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
            GroupId = "basket-api",
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

                    using var scope = scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IBasketRepository>();
                    repository.DeleteCartAsync(stockReserved.UserId).GetAwaiter().GetResult();

                    logger.LogInformation("Carrito de {UserId} vaciado: orden {OrderId} confirmada", stockReserved.UserId, stockReserved.OrderId);
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
}
