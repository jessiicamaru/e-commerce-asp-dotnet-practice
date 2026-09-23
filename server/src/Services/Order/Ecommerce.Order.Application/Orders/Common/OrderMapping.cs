using Ecommerce.Order.Domain.Enums;

namespace Ecommerce.Order.Application.Orders.Common;

public static class OrderMapping
{
    /// <summary>
    /// What an order's status means to anyone reading it.
    /// </summary>
    /// <remarks>
    /// <c>Completed</c> is no longer written (feature 011): a successful checkout now settles to
    /// <c>Paid</c>, which is what the word always meant. It stays in the enum so rows written before -
    /// or by an earlier image, if one is redeployed - still parse, and it is reported as <c>Paid</c>.
    /// </remarks>
    public static string Describe(OrderStatus status) =>
        status == OrderStatus.Completed ? nameof(OrderStatus.Paid) : status.ToString();

    /// <summary>
    /// What a part's status means to anyone reading it (specs/035). A part nobody has started is part of
    /// a paid order, and is read as <c>Paid</c> - so a client needs no new word, and an order in one
    /// parcel reads exactly as it did before parts existed.
    /// </summary>
    public static string Describe(ShipmentStatus status) =>
        status == ShipmentStatus.Pending ? nameof(OrderStatus.Paid) : status.ToString();

    /// <summary>
    /// The order's parts for its customer: each parcel's goods, state and tracking reference. The shop's
    /// part first, then the sellers' in a fixed order, so the page does not reshuffle between reads.
    /// </summary>
    public static List<ShipmentResponse> ToShipments(Domain.Entities.Order order) =>
        order.Shipments
            .OrderBy(s => s.SellerId is not null)
            .ThenBy(s => s.SellerId)
            .Select(s => new ShipmentResponse(
                Describe(s.Status),
                s.TrackingReference,
                order.Items
                    .Where(i => i.SellerId == s.SellerId)
                    .Select(i => string.IsNullOrEmpty(i.OptionSummary) ? i.ProductName : $"{i.ProductName} · {i.OptionSummary}")
                    .ToList(),
                // Who sends it (specs/036): the name frozen on its lines. Every line of one part is the
                // same seller's, so any recorded one will do; none recorded is null, not a guess.
                order.Items.Where(i => i.SellerId == s.SellerId).Select(i => i.SellerName).FirstOrDefault(n => n is not null),
                IsShop: s.SellerId is null))
            .ToList();

    public static ShippingAddressResponse? ToResponse(Domain.Entities.ShippingAddress? a) =>
        a is null ? null : new ShippingAddressResponse(
            a.RecipientName, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country, a.Phone);

    public static OrderDetailResponse ToDetail(Domain.Entities.Order order) => new(
        order.Id,
        order.UserId,
        order.TotalAmount,
        Describe(order.Status),
        order.FailureReason,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items
            .Select(x => new OrderItemDetailResponse(
                x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.TotalPrice, x.TaxAmount,
                x.VariantId, x.Sku, x.OptionSummary, x.SellerName))
            .ToList(),
        ToResponse(order.ShipTo),
        order.ShippingOptionCode is null
            ? null
            : new ShippingOptionResponse(
                order.ShippingOptionCode,
                order.ShippingOptionName ?? order.ShippingOptionCode,
                // What delivery cost on THIS order, not what it costs today, and in the order's own
                // currency. Reading it back from configuration would re-price a finished purchase.
                order.ShippingPrice,
                order.Currency ?? string.Empty),
        order.ShippingPrice,
        order.TrackingReference,
        order.Subtotal,
        order.TaxTotal,
        order.DiscountTotal,
        order.TaxRate,
        // Frozen at checkout. An order placed in dong still reads in dong when it is opened by
        // somebody browsing in dollars, because an order is a record of a purchase (specs/022).
        order.Currency ?? string.Empty,
        order.Language ?? string.Empty,
        ToShipments(order));
}
