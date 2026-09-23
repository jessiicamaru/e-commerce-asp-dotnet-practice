using System.Text.Json;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Domain.Entities;
using Ecommerce.Contracts.Activity;
using MediatR;

namespace Ecommerce.Activity.Application.Audit.Commands;

/// <summary>Keeps one audit entry, once (specs/041). True when THIS call recorded it.</summary>
public record RecordAuditEntryCommand(AuditEntryRecorded Entry) : IRequest<bool>;

public class RecordAuditEntryCommandHandler(IAuditRepository audit) : IRequestHandler<RecordAuditEntryCommand, bool>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAuditRepository _audit = audit;

    public Task<bool> Handle(RecordAuditEntryCommand request, CancellationToken cancellationToken)
    {
        var e = request.Entry;

        // The diff is worked out here, once, and stored - the page never recomputes it (research D5).
        var changes = AuditDiff.Compute(e.Before, e.After);

        return _audit.TryAddAsync(new AuditEntry
        {
            Id = e.EntryId,
            Category = e.Category,
            Action = e.Action,
            ActorId = e.ActorId,
            ActorEmail = e.ActorEmail,
            ActorRole = e.ActorRole,
            SubjectType = e.SubjectType,
            SubjectId = e.SubjectId,
            Summary = e.Summary,
            Before = e.Before,
            After = e.After,
            Changes = JsonSerializer.Serialize(changes, Json),
            ChangeCount = changes.Count,
            Service = e.Service,
            OccurredAt = e.OccurredAt,
            RecordedAt = DateTime.UtcNow
        }, cancellationToken);
    }
}
