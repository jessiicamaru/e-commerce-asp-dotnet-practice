using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.ConfirmStock;

public record ConfirmStockCommand(Guid OrderId) : IRequest<int>;
