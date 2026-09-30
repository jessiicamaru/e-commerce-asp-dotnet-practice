using Ecommerce.Cart.Application.MyData;
using Ecommerce.Contracts.Identity;
using MassTransit;
using MediatR;

namespace Ecommerce.Cart.WebApi.Consumers;

/// <summary>
/// An account was deleted (specs/112): Cart takes the person out of its rows. Named for what it does - a consumer's
/// class name is its queue name, and four services consume this message.
/// </summary>
public class EraseAccountFromCartConsumer(ISender mediator) : IConsumer<AccountDeleted>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<AccountDeleted> context) =>
        _mediator.Send(new EraseAccountCommand(context.Message.UserId, context.Message.Email), context.CancellationToken);
}
