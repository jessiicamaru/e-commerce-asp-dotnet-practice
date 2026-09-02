namespace Ecommerce.Contracts.Payment;

public record ProcessPaymentCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount
);

public record PaymentProcessedEvent(
    Guid OrderId,
    Guid PaymentId,
    DateTime ProcessedAt
);

public record PaymentFailedEvent(
    Guid OrderId,
    string Reason
);
