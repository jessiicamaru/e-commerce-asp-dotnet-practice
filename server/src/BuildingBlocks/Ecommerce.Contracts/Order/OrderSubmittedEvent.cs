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

public record OrderSubmittedEvent(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    List<OrderItemDto> Items,
    DateTime CreatedAt
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
