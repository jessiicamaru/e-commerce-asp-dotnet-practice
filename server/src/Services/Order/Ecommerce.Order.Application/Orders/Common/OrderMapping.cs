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
            .Select(x => new OrderItemDetailResponse(x.ProductId, x.ProductName, x.Quantity, x.UnitPrice, x.TotalPrice, x.TaxAmount))
            .ToList(),
        ToResponse(order.ShipTo),
        order.ShippingOptionCode is null
            ? null
            : new ShippingOptionResponse(order.ShippingOptionCode, order.ShippingOptionName ?? order.ShippingOptionCode),
        order.ShippingPrice,
        order.TrackingReference,
        order.Subtotal,
        order.TaxTotal,
        order.DiscountTotal,
        order.TaxRate);
}
