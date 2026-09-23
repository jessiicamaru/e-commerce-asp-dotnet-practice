namespace Ecommerce.Contracts.Order;

/// <param name="VariantId">
/// Which shape of the product was bought - the sellable unit (specs/020). <b>Additive</b>: a message
/// from an image built before variants existed carries <c>Guid.Empty</c>, and a consumer reads that as
/// the variant whose id IS the product id, which is what the backfill made true for every product that
/// existed (specs/020 research D2).
/// </param>
/// <param name="ProductId">Kept: it is what a consumer falls back to, and what a link points at.</param>
public record OrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    Guid VariantId = default
);

/// <param name="Currency">
/// What <paramref name="TotalAmount"/> and the lines are denominated in (specs/022). <b>Additive</b>:
/// an empty string is what an Order built before this feature sends, and every consumer reads that as
/// the shop's default currency - which on the day of the deploy is the truth, not a guess.
///
/// ⚠️ The Orchestrator RELAYS this into <c>ProcessPaymentCommand</c>. An Orchestrator image built
/// before this change deserialises the event into its older record, drops this field, and charges the
/// right number in the wrong money - and nothing downstream can detect it, because 899 is a valid
/// amount in both currencies. This is the same omission that moved the wrong variant's stock in
/// specs/020. Rebuild every image together.
/// </param>
public record OrderSubmittedEvent(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    List<OrderItemDto> Items,
    DateTime CreatedAt,
    string Currency = ""
);

public record OrderCompletedEvent(
    Guid OrderId,
    DateTime CompletedAt
);

public record OrderFailedEvent(
    Guid OrderId,
    string Reason,
    DateTime FailedAt
);

/// <summary>
/// A paid order was cancelled before anything shipped (specs/039). Inventory puts its stock back and
/// Payment records a refund - each from its own rows, which is why this carries no items and no amount.
/// </summary>
/// <param name="CancelledBy">"Customer" or "Staff".</param>
public record OrderCancelledEvent(
    Guid OrderId,
    DateTime CancelledAt,
    string CancelledBy
);
