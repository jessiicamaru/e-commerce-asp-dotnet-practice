using Ecommerce.Application.Email;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Email;

public class OutgoingEmailRepository(ApplicationDbContext context) : IOutgoingEmailRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<bool> QueueAsync(OutgoingEmail email, CancellationToken cancellationToken = default) =>
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO outgoing_emails ("Id", "RecipientId", "Template", "DataJson", "Language", "Status", "Attempts", "NextAttemptAt", "CreatedAt")
            VALUES ({email.Id}, {email.RecipientId}, {email.Template}, CAST({email.DataJson} AS jsonb), {email.Language},
                    {email.Status.ToString()}, 0, {email.NextAttemptAt}, {email.CreatedAt})
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken) == 1;

    public void Stage(OutgoingEmail email) => _context.OutgoingEmails.Add(email);

    public Task<List<OutgoingEmail>> ClaimDueAsync(DateTime now, int batch, CancellationToken cancellationToken = default) =>
        _context.OutgoingEmails
            .FromSqlInterpolated($"""
                SELECT * FROM outgoing_emails
                WHERE "Status" = 'Pending' AND "NextAttemptAt" <= {now}
                ORDER BY "NextAttemptAt"
                LIMIT {batch}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

    public Task<OutgoingEmail?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.OutgoingEmails.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<(OutgoingEmail Email, string? RecipientEmail)?> GetWithRecipientAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await WithRecipient(_context.OutgoingEmails.AsNoTracking().Where(e => e.Id == id)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.Email, row.RecipientEmail);
    }

    public async Task<(List<(OutgoingEmail Email, string? RecipientEmail)> Items, int Total)> PageAsync(
        OutgoingEmailStatus status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = WithRecipient(_context.OutgoingEmails.AsNoTracking().Where(e => e.Status == status));
        if (search is not null)
        {
            var key = search.ToLowerInvariant();
            query = query.Where(x => x.RecipientEmail != null && x.RecipientEmail.ToLower().Contains(key));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.Email.CreatedAt).ThenByDescending(x => x.Email.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (rows.Select(r => (r.Email, r.RecipientEmail)).ToList(), total);
    }

    public async Task<bool> TryRetryAsync(Guid id, DateTime now, CancellationToken cancellationToken = default) =>
        await _context.OutgoingEmails
            .Where(e => e.Id == id && e.Status == OutgoingEmailStatus.Failed)
            .ExecuteUpdateAsync(set => set
                .SetProperty(e => e.Status, OutgoingEmailStatus.Pending)
                .SetProperty(e => e.Attempts, 0)
                .SetProperty(e => e.NextAttemptAt, now)
                .SetProperty(e => e.LastError, (string?)null), cancellationToken) == 1;

    /// <summary>A left join: an email whose recipient is gone still shows, with no address.</summary>
    private IQueryable<EmailWithRecipient> WithRecipient(IQueryable<OutgoingEmail> emails) =>
        from e in emails
        join u in _context.Users on e.RecipientId equals u.Id into recipients
        from u in recipients.DefaultIfEmpty()
        select new EmailWithRecipient { Email = e, RecipientEmail = u == null ? null : u.Email };

    private sealed class EmailWithRecipient
    {
        public OutgoingEmail Email { get; init; } = null!;
        public string? RecipientEmail { get; init; }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
