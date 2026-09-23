namespace Ecommerce.Contracts.Order;

/// <summary>
/// A parcel reached its customer (specs/040) - confirmed by them, or taken as delivered after the window.
/// Published by Order; Catalog keeps who received which product, which is who may review it (specs/046).
/// </summary>
/// <remarks>
/// The product ids, not the variants: a review is of a camera, not of its black body-only shape. One
/// event per parcel, so a customer who received half an order may review that half.
/// </remarks>
public record ParcelDeliveredEvent(
    Guid OrderId,
    Guid ShipmentId,
    Guid BuyerId,
    List<Guid> ProductIds,
    DateTime DeliveredAt);
