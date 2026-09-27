using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.RestockCancelledOrder;

/// <summary>
/// A paid order was cancelled (specs/039): put back what it took. Returns how many reservations it settled.
/// </summary>
/// <param name="VariantIds">
/// Only these variants' reservations - one part of the order, cancelled on its own (specs/104). Null: the whole order.
/// A variant belongs to one seller, so its reservation belongs wholly to that seller's part.
/// </param>
public record RestockCancelledOrderCommand(Guid OrderId, List<Guid>? VariantIds = null) : IRequest<int>;
