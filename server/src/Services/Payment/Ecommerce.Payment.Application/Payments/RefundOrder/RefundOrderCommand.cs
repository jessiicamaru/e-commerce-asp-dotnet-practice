using MediatR;

namespace Ecommerce.Payment.Application.Payments.RefundOrder;

/// <summary>
/// A paid order was cancelled (specs/039): record giving back what was charged. True when THIS call
/// recorded the refund; false when there was nothing to refund, or it already had been.
/// </summary>
public record RefundOrderCommand(Guid OrderId) : IRequest<bool>;
