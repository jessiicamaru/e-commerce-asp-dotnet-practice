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
    decimal TotalPrice
);

public record OrderResponse(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt,
    List<OrderItemResponse> Items
);

// UserId is intentionally absent: it is read from the access token, never from the request body.
public record SubmitOrderCommand(
    List<OrderItemRequest> Items
) : IRequest<OrderResponse>;
