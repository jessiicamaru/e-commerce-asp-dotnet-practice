namespace Ecommerce.Contracts.Order;

/// <summary>
/// A returned parcel has come back to its seller (specs/066, #107). Published by Order in the transaction that
/// marks the return received; consumed by Payment (a refund of <paramref name="Amount"/>) and Inventory (the
/// units back on the shelf). Both are idempotent on <paramref name="ReturnId"/>.
/// </summary>
/// <remarks>
/// Unlike <c>OrderCancelledEvent</c>, which carries nothing, this carries its lines and its amount: a return is
/// PART of an order, and only Order knows which lines are that part and what was paid for them - the prices
/// frozen at checkout, goods plus their tax, never the delivery.
/// </remarks>
/// <param name="Items">What comes back: the variant (or, for a line from before variants, the product) and how many.</param>
public record ParcelReturnedEvent(
    Guid ReturnId,
    Guid OrderId,
    Guid ShipmentId,
    List<ReturnedItemDto> Items,
    decimal Amount,
    string Currency,
    DateTime ReturnedAt);

/// <param name="VariantId">What Inventory's stock row is keyed by - a variant id (specs/020).</param>
public record ReturnedItemDto(Guid VariantId, int Quantity);
