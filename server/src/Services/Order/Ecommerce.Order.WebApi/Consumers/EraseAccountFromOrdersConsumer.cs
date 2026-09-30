using Ecommerce.Contracts.Identity;
using Ecommerce.Order.Application.MyData;
using MassTransit;
using MediatR;

namespace Ecommerce.Order.WebApi.Consumers;

/// <summary>
/// An account was deleted (specs/112): Order keeps its orders for the books, without the person's name, street or
/// phone. Named for what it does - a consumer's class name is its queue name, and four services consume this message.
/// </summary>
public class EraseAccountFromOrdersConsumer(ISender mediator) : IConsumer<AccountDeleted>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<AccountDeleted> context) =>
        _mediator.Send(new EraseAccountCommand(context.Message.UserId), context.CancellationToken);
}
