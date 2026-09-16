using Ecommerce.Inventory.Application.Common.Interfaces;
using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.Queries.GetReservationsByOrderId;

public class GetReservationsByOrderIdQueryHandler(IReservationRepository reservationRepository)
    : IRequestHandler<GetReservationsByOrderIdQuery, OrderReservationsResponse>
{
    private readonly IReservationRepository _reservationRepository = reservationRepository;

    public async Task<OrderReservationsResponse> Handle(
        GetReservationsByOrderIdQuery request,
        CancellationToken cancellationToken)
    {
        var reservations = await _reservationRepository
            .GetByOrderIdAsync(request.OrderId, cancellationToken);

        // An unknown order returns an empty list rather than a 404: asking about an order that
        // holds nothing is a legitimate question with a legitimate answer.
        return new OrderReservationsResponse(
            request.OrderId,
            reservations
                .Select(x => new ReservationResponse(
                    x.ProductId,
                    x.Quantity,
                    x.Status.ToString(),
                    x.ExpiresAt,
                    x.CreatedAt,
                    x.SettledAt,
                    x.SettlementReason))
                .ToList());
    }
}
