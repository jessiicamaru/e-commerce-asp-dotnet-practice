using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;

/// <summary>
/// A paid order was cancelled (specs/039): put back what it took. Returns how many reservations it settled.
/// </summary>
public record RestockCancelledOrderCommand(Guid OrderId) : IRequest<int>;
