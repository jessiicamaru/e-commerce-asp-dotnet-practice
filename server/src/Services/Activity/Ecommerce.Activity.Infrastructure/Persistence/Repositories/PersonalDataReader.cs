using Ecommerce.Activity.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Activity.Infrastructure.Persistence.Repositories;

/// <summary>
/// The person's notices and their lines in the audit log, as handed out (#217, specs/111). ⚠️ An entry is theirs when
/// they did it or it was decided about them - and neither way carries the actor's identity or the before/after
/// snapshots: the staff member who decided is not the person's data, and a snapshot can hold somebody else's.
/// </summary>
public class PersonalDataReader(ActivityDbContext context) : IPersonalDataReader
{
    private readonly ActivityDbContext _context = context;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Notifications.AsNoTracking().Where(n => n.RecipientId == userId).OrderBy(n => n.CreatedAt)
            .Select(n => new { n.Id, n.Kind, n.Data, n.Link, n.CreatedAt, n.ReadAt })
            .ToListAsync(cancellationToken);

        var activity = await _context.AuditEntries.AsNoTracking()
            .Where(e => e.ActorId == userId || e.AboutUserId == userId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new
            {
                e.Id, ByYou = e.ActorId == userId, e.Service, e.Category, e.Action, e.SubjectType, e.SubjectId, e.Summary, e.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        return new Dictionary<string, IReadOnlyList<object>>
        {
            ["notifications"] = notifications.Cast<object>().ToList(),
            ["activity"] = activity.Cast<object>().ToList(),
        };
    }
}
