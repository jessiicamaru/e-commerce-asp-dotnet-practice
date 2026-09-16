using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.ReserveStock;

public record ReserveStockItem(Guid ProductId, int Quantity);

public record ReserveStockResult(bool Succeeded, string? FailureReason)
{
    public static ReserveStockResult Success() => new(true, null);

    public static ReserveStockResult Failure(string reason) => new(false, reason);
}

public record ReserveStockCommand(
    Guid OrderId,
    List<ReserveStockItem> Items
) : IRequest<ReserveStockResult>;
