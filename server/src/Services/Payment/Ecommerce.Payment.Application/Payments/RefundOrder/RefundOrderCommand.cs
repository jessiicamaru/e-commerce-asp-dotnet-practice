using MediatR;

namespace Ecommerce.Payment.Application.Payments.RefundOrder;

/// <summary>
/// Record giving back what was charged for an order: a paid order was cancelled (specs/039), or its payment
/// was approved after the saga had already failed it (specs/053). True when THIS call recorded the refund;
/// false when there was nothing to refund, or it already had been.
/// </summary>
/// <param name="Reason">Why, for the audit log.</param>
public record RefundOrderCommand(Guid OrderId, string Reason = RefundOrderCommand.Cancelled) : IRequest<bool>
{
    public const string Cancelled = "the order was cancelled";
}
