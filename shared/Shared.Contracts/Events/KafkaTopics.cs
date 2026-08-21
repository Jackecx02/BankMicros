namespace Shared.Contracts.Events;

public static class KafkaTopics
{
    public const string OrderCreated = "order-created";
    public const string StockReserved = "stock-reserved";
    public const string StockReservationFailed = "stock-reservation-failed";
}
