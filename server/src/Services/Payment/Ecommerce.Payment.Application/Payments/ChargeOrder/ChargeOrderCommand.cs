using MediatR;

namespace Ecommerce.Payment.Application.Payments.ChargeOrder;

/// <param name="AwaitingCustomer">
/// Nothing decided yet: a redirect gateway's checkout is open and the customer pays there (specs/143). The saga keeps
/// waiting, and the gateway's notification decides.
/// </param>
public record ChargeOrderResult(Guid PaymentId, bool Approved, string? FailureReason, bool AwaitingCustomer = false);

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
