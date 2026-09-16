using MediatR;

namespace Ecommerce.Payment.Application.Payments.ChargeOrder;

public record ChargeOrderResult(Guid PaymentId, bool Approved, string? FailureReason);

public record ChargeOrderCommand(
    Guid OrderId,
    Guid UserId,
    decimal Amount
) : IRequest<ChargeOrderResult>;
