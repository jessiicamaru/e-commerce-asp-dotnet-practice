namespace Ecommerce.Contracts.Inventory;

/// <summary>
/// Inventory announcing that a product's buyability has changed. Published from every path that
/// moves stock — reservation, release, confirmation, expiry, an administrator's adjustment, and the
/// registration that creates the stock item at zero.
/// </summary>
/// <param name="QuantityAvailable">
/// What a new shopper could buy: on hand minus held. Carried because the owner already has it and a
/// later subscriber — a low-stock alerter, a report — would otherwise need a second contract.
/// <b>Catalog deliberately does not store it</b>; doing so would recreate the duplicated stock
/// figure this event exists to remove.
/// </param>
/// <param name="IsAvailable">
/// Derived by the owner. Subscribers must not re-derive it from <paramref name="QuantityAvailable"/>
/// — if what counts as "available" ever changes, it changes in one place.
/// </param>
/// <param name="ObservedAt">
/// When Inventory observed this, <b>not</b> when the message was sent or received. It is what lets a
/// consumer discard an announcement that a newer one has overtaken: the broker preserves order only
/// in the absence of redelivery, and redelivery is normal operation.
/// </param>
/// <param name="VariantId">
/// The sellable unit this is about (specs/020). Stock is counted per variant, so availability is too:
/// a black body can be in stock while a silver kit is not. <b>Additive</b> - an older publisher sends
/// nothing and the consumer falls back to <paramref name="ProductId"/>, which for every product that
/// existed before variants IS its only variant's id (specs/020 research D2).
/// </param>
public record StockAvailabilityChangedEvent(
    Guid ProductId,
    int QuantityAvailable,
    bool IsAvailable,
    DateTime ObservedAt,
    Guid VariantId = default
);
