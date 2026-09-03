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

public record SubmitOrderCommand(
    Guid UserId,
    List<OrderItemRequest> Items
) : IRequest<OrderResponse>;
