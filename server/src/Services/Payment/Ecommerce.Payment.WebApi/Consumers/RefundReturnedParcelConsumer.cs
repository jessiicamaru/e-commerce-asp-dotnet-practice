using Ecommerce.Contracts.Order;
using Ecommerce.Payment.Application.Payments.RefundReturn;
using MassTransit;
using MediatR;

namespace Ecommerce.Payment.WebApi.Consumers;

/// <summary>
/// A returned parcel came back to its seller (specs/066): record its refund.
/// </summary>
/// <remarks>
/// ⚠️ Named for what it does - Inventory consumes the same event, and a shared class name would be a shared
/// queue (CLAUDE.md gotcha).
/// </remarks>
public class RefundReturnedParcelConsumer(ISender mediator) : IConsumer<ParcelReturnedEvent>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<ParcelReturnedEvent> context) =>
        _mediator.Send(new RefundReturnCommand(context.Message.ReturnId, context.Message.OrderId,
            context.Message.Amount, context.Message.Currency), context.CancellationToken);
}
