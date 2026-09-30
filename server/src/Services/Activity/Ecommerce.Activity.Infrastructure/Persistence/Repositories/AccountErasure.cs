using Ecommerce.Activity.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence.Repositories;

/// <summary>What Activity erases when an account is deleted (specs/112).</summary>
/// <remarks>
/// The audit log is the security record, so its entries stay: who did what to which subject, when. What goes is the
/// person in them - their email as an actor, their email where a summary was written with it ("x@y changed their
/// details"), and the before/after snapshots of their own user row, which held their name and phone.
/// </remarks>
public class AccountErasure(ActivityDbContext context) : IAccountErasure
{
    /// <summary>What a summary says where the email was.</summary>
    public const string InPlaceOfTheEmail = "a deleted account";

    private readonly ActivityDbContext _context = context;

    public async Task EraseAsync(Guid personId, string email, CancellationToken cancellationToken = default)
    {
        // Inside a consumer the outbox may hold a transaction already: take part in it.
        if (_context.Database.CurrentTransaction is not null)
        {
            await EraseInTransactionAsync(personId, email, cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await EraseInTransactionAsync(personId, email, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task EraseInTransactionAsync(Guid personId, string email, CancellationToken cancellationToken)
    {
        var subject = personId.ToString();

        await _context.Notifications.Where(n => n.RecipientId == personId).ExecuteDeleteAsync(cancellationToken);
        await _context.AuditEntries.Where(e => e.ActorId == personId)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.ActorEmail, (string?)null), cancellationToken);
        if (!string.IsNullOrEmpty(email))
        {
            await _context.AuditEntries
                .Where(e => (e.ActorId == personId || e.AboutUserId == personId || e.SubjectId == subject) && e.Summary.Contains(email))
                .ExecuteUpdateAsync(x => x.SetProperty(e => e.Summary, e => e.Summary.Replace(email, InPlaceOfTheEmail)), cancellationToken);
        }

        await _context.AuditEntries.Where(e => e.SubjectType == "User" && e.SubjectId == subject)
            .ExecuteUpdateAsync(x => x
                .SetProperty(e => e.Before, (string?)null)
                .SetProperty(e => e.After, (string?)null)
                .SetProperty(e => e.Changes, "[]")
                .SetProperty(e => e.ChangeCount, 0), cancellationToken);
    }
}
