using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.ReleaseStock;

public record ReleaseStockCommand(Guid OrderId, string Reason) : IRequest<int>;
