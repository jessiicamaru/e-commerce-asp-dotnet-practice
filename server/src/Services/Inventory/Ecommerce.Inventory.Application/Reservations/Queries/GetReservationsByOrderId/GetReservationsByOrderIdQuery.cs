using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.Queries.GetReservationsByOrderId;

public record ReservationResponse(
    Guid ProductId,
    int Quantity,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? SettledAt,
    string? SettlementReason
);

public record OrderReservationsResponse(Guid OrderId, List<ReservationResponse> Reservations);

public record GetReservationsByOrderIdQuery(Guid OrderId) : IRequest<OrderReservationsResponse>;
