namespace Shared.Contracts.Events;

public record StockReservedEvent(Guid OrderId, Guid UserId);

public record StockReservationFailedEvent(Guid OrderId, Guid UserId, string Reason);
