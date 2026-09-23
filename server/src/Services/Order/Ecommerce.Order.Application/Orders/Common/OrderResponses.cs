namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>
/// One row in a shopper's order list.
/// </summary>
/// <remarks>
/// Carries <paramref name="ItemCount"/> rather than the items themselves. A shopper scanning their
/// orders does not need every product on every one, and loading them would make a page cost grow
/// with the size of the orders on it instead of with the size of the page.
/// </remarks>
public record OrderSummaryResponse(
    Guid OrderId,
    decimal TotalAmount,
    string Status,
    string? FailureReason,
    int ItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Currency = "",
    string Language = "",
    int ShipmentCount = 0,
    int ShipmentsShipped = 0
);

/// <summary>
/// One part of an order - one parcel (specs/035). The customer sees each: what is in it, where it has
/// got to, and its tracking reference. Status is <c>Paid</c> while nobody has started it.
/// </summary>
/// <param name="Items">What is in this parcel, in the words frozen on the order's lines.</param>
public record ShipmentResponse(string Status, string? TrackingReference, List<string> Items);

public record ShippingAddressResponse(
    string RecipientName,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    string Country,
    string? Phone
);

/// <param name="Price">
/// What it costs in <paramref name="Currency"/>. Only options priced in the request's currency are
/// offered at all (specs/022 FR-008), so this is never null on an offered option.
/// </param>
public record ShippingOptionResponse(string Code, string Name, decimal? Price = null, string Currency = "");

/// <param name="VariantId">Which shape of the product was bought; null on lines from before variants.</param>
/// <param name="OptionSummary">What the customer chose, in words, as frozen at purchase (specs/020).</param>
public record OrderItemDetailResponse(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    decimal? TaxAmount = null,
    Guid? VariantId = null,
    string? Sku = null,
    string? OptionSummary = null
);

/// <summary>
/// One order in full. <paramref name="UserId"/> is echoed because it is always the caller's own, so
/// it identifies nobody they do not already know about.
/// </summary>
/// <param name="Currency">
/// The currency every amount here is in (specs/022). Empty on orders placed before that feature,
/// which are in the shop's default - stated as empty rather than guessed at, so a reader can tell
/// "placed in dong" from "nobody recorded it".
/// </param>
/// <param name="Language">
/// The language the frozen words on the lines are in (specs/021). Empty for the same reason.
/// </param>
public record OrderDetailResponse(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    string Status,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<OrderItemDetailResponse> Items,
    ShippingAddressResponse? ShippingAddress = null,
    ShippingOptionResponse? ShippingOption = null,
    decimal? ShippingPrice = null,
    string? TrackingReference = null,
    decimal? Subtotal = null,
    decimal? TaxTotal = null,
    decimal? DiscountTotal = null,
    decimal? TaxRate = null,
    string Currency = "",
    string Language = "",
    List<ShipmentResponse>? Shipments = null
);

/// <summary>
/// A page of results. <paramref name="TotalCount"/> is the caller's own total, counted before
/// paging, so a page past the end comes back empty while still reporting the honest total.
/// </summary>
public record PagedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);
