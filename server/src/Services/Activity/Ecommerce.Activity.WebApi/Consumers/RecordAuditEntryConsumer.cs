using Ecommerce.Activity.Application.Audit.Commands;
using Ecommerce.Contracts.Activity;
using MassTransit;
using MediatR;

namespace Ecommerce.Activity.WebApi.Consumers;

/// <summary>Keeps every audit entry any service reports (specs/041).</summary>
public class RecordAuditEntryConsumer(ISender mediator) : IConsumer<AuditEntryRecorded>
{
    private readonly ISender _mediator = mediator;

    public Task Consume(ConsumeContext<AuditEntryRecorded> context) =>
        _mediator.Send(new RecordAuditEntryCommand(context.Message), context.CancellationToken);
}
