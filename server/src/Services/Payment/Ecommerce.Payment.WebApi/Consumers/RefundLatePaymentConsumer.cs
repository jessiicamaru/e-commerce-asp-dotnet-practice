using Ecommerce.Contracts.Payment;
using Ecommerce.Payment.Application.Payments.RefundOrder;
using MassTransit;
using MediatR;

namespace Ecommerce.Payment.WebApi.Consumers;

/// <summary>
/// The saga failed an order waiting for its payment, and the payment was approved afterwards (specs/053):
/// record the refund, through the same once-only path as a cancellation.
/// </summary>
/// <remarks>
/// ⚠️ Named for what it does: the class name is the queue name (CLAUDE.md gotcha).
/// </remarks>
public class RefundLatePaymentConsumer(ISender mediator) : IConsumer<RefundPaymentCommand>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<RefundPaymentCommand> context) =>
        _mediator.Send(new RefundOrderCommand(context.Message.OrderId, context.Message.Reason), context.CancellationToken);
}
