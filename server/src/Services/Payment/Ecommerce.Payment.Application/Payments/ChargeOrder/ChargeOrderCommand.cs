using MediatR;

namespace Ecommerce.Payment.Application.Payments.ChargeOrder;

public record ChargeOrderResult(Guid PaymentId, bool Approved, string? FailureReason);

/// <param name="Currency">
/// What <paramref name="Amount"/> is denominated in (specs/022). Empty means the shop's default -
/// what a saga built before that feature sends, and what an in-flight message carries on the day it
/// is deployed.
/// </param>
public record ChargeOrderCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency = ""
) : IRequest<ChargeOrderResult>;
