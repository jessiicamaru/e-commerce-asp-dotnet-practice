namespace Ecommerce.Payment.Application.Payments.Common;

public record PaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Status,
    string? FailureReason,
    string Provider,
    DateTime ProcessedAt
);
