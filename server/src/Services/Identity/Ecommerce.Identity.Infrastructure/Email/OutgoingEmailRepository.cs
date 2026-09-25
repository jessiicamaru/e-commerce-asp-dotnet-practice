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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
