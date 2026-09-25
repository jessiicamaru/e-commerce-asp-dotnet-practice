using Ecommerce.Application.Email;
using Ecommerce.Contracts.Identity;
using MassTransit;
using MediatR;

namespace Ecommerce.WebApi.Consumers;

/// <summary>
/// Keeps an email another service asked for (specs/060); the dispatcher sends it.
/// </summary>
/// <remarks>
/// Named for what it does - the class name is the queue name (CLAUDE.md gotcha). It only stores: sending
/// here would tie the broker's delivery to the mail server's mood, and a mail server that is down would
/// then fill the error queue instead of waiting in a table.
/// </remarks>
public class QueueEmailConsumer(ISender mediator) : IConsumer<EmailRequested>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<EmailRequested> context) =>
        _mediator.Send(new QueueEmailCommand(context.Message), context.CancellationToken);
}
