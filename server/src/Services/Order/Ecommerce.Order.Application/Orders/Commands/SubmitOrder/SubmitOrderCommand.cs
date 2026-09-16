using MediatR;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

public record OrderItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
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
