namespace Ecommerce.Contracts.Order;

public record OrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
);

public record OrderSubmittedEvent(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    List<OrderItemDto> Items,
    DateTime CreatedAt
);

public record OrderCompletedEvent(
    Guid OrderId,
    DateTime CompletedAt
);

public record OrderFailedEvent(
    Guid OrderId,
    string Reason,
    DateTime FailedAt
);
