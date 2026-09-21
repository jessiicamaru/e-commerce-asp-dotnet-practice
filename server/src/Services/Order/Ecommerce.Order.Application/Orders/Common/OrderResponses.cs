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
    DateTime UpdatedAt
);

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

public record ShippingOptionResponse(string Code, string Name);

public record OrderItemDetailResponse(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

/// <summary>
/// One order in full. <paramref name="UserId"/> is echoed because it is always the caller's own, so
/// it identifies nobody they do not already know about.
/// </summary>
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
    string? TrackingReference = null
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
