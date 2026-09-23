using Ecommerce.Activity.Application.Notifications;
using Ecommerce.Contracts.Activity;
using MassTransit;
using MediatR;

namespace Ecommerce.Activity.WebApi.Consumers;

/// <summary>Puts every notification any service sends into its recipient's inbox (specs/042).</summary>
public class RecordNotificationConsumer(ISender mediator) : IConsumer<UserNotificationRequested>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<UserNotificationRequested> context) =>
        _mediator.Send(new RecordNotificationCommand(context.Message), context.CancellationToken);
}
