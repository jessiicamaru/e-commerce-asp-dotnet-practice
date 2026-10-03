namespace Ecommerce.Payment.Application.Payments.Common;

public record PaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Status,
    string? FailureReason,
    string Provider,
    DateTime ProcessedAt,
    decimal? RefundedAmount = null,
    DateTime? RefundedAt = null,
    // The gateway's own transaction number (specs/143) - what to search for in VNPay's merchant portal; null for the stub.
    string? ProviderReference = null
);
