using Ecommerce.Application.Auth.Handoff;
using Ecommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class BackOfficeHandoffRepository(ApplicationDbContext context) : IBackOfficeHandoffRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task AddAsync(BackOfficeHandoff handoff, CancellationToken cancellationToken = default) =>
        await _context.BackOfficeHandoffs.AddAsync(handoff, cancellationToken);

    public async Task<Guid?> TryClaimAsync(string codeHash, DateTime now, CancellationToken cancellationToken = default)
    {
        var claimed = await _context.Database.SqlQuery<Guid>($"""
            UPDATE back_office_handoffs SET "UsedAt" = {now}
            WHERE "CodeHash" = {codeHash} AND "UsedAt" IS NULL AND "ExpiresAt" > {now}
            RETURNING "UserId" AS "Value"
            """).ToListAsync(cancellationToken);

        return claimed.Count == 1 ? claimed[0] : null;
    }
}
