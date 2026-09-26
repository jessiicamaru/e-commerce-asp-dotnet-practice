using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

/// <summary>
/// What the customer is asking for: which product, and how many.
/// </summary>
/// <remarks>
/// <b>ProductName and UnitPrice are deliberately absent.</b> They used to be here, taken from the
/// request body and written straight onto the order line — issue #18, where a product listed at
/// 40,000,000 was bought for 1, the stock was permanently deducted, and the payment recorded
/// <c>1.00 Approved</c>. Both now come from Catalog, which owns them.
/// <para>
/// Removed rather than ignored, for the same reason <c>UserId</c> was removed from
/// <see cref="SubmitOrderCommand"/>: a field the server accepts and discards reads as supported in
/// every client that sees it, and is an invitation for somebody to start honouring it again.
/// </para>
/// </remarks>
public record OrderItemRequest(
    Guid ProductId,
    int Quantity
);

public record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    decimal? TaxAmount = null,
    Guid? VariantId = null,
    string? Sku = null,
    string? OptionSummary = null,
    string? SellerName = null,
    decimal Discount = 0m
);

public record OrderResponse(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt,
    List<OrderItemResponse> Items,
    Common.ShippingAddressResponse? ShippingAddress = null,
    Common.ShippingOptionResponse? ShippingOption = null,
    decimal? ShippingPrice = null,
    decimal? Subtotal = null,
    decimal? TaxTotal = null,
    decimal? DiscountTotal = null,
    decimal? TaxRate = null,
    string Currency = "",
    List<Common.AppliedVoucherResponse>? Vouchers = null
);

/// <summary>
/// Check out the caller's cart, to one of their addresses, by a delivery option they chose.
/// </summary>
/// <param name="AddressId">One of the caller's addresses in Identity; <c>null</c> for their default.</param>
/// <param name="ShippingOption">A delivery option code - required, because it costs money.</param>
/// <remarks>
/// <para>
/// <b>Carries only choices the customer owns</b> - where to send it and how (feature 011). Not the items - they come from the caller's cart, read from
/// the Cart service at checkout (feature 010). Not a user id - that comes from the access token, never
/// from the request body (Constitution IV). Not a price or a name - those come from Catalog (feature
/// 009, issue #18).
/// </para>
/// <para>
/// Each of those fields once existed, or could have, and each would have let the client assert
/// something the server owns.
/// </para>
/// </remarks>
/// <param name="VoucherCodes">
/// Codes the customer typed (specs/069) - a request, never a discount: what each takes off is worked out by
/// the server, which may refuse any of them.
/// </param>
public record SubmitOrderCommand(Guid? AddressId, string ShippingOption, IReadOnlyList<string>? VoucherCodes = null) : IRequest<OrderResponse>;
