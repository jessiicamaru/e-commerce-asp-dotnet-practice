using MediatR;

namespace Ecommerce.Inventory.Application.Reservations.ReserveStock;

/// <param name="ProductId">
/// The sellable unit to hold - a <b>variant</b> id since specs/020, under a name kept for the same
/// reason the column is (research D9).
/// </param>
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
