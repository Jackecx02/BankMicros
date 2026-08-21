using Catalog.Api.Data;
using Catalog.Api.Endpoints;
using Catalog.Api.Messaging;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CatalogDb")));

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
    };
    return new ProducerBuilder<string, string>(config).Build();
});
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    CatalogDbSeeder.Seed(db);
}

app.UseHttpsRedirection();
app.MapProductEndpoints();

app.Run();
