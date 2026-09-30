using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>What Catalog erases when an account is deleted (specs/112).</summary>
/// <remarks>
/// A review or question keeps its text and rating - other shoppers rely on them, and a seller's answer hangs on a
/// question - and loses its author's name; the storefront words an empty name "a former customer". The product's
/// rating is not recomputed: no rating changed.
/// </remarks>
public class AccountErasure(CatalogDbContext context, ISellerRepository sellers) : IAccountErasure
{
    private readonly CatalogDbContext _context = context;
    private readonly ISellerRepository _sellers = sellers;

    public async Task EraseAsync(Guid personId, DateTime deletedAt, CancellationToken cancellationToken = default)
    {
        // Inside a consumer the outbox holds a transaction already: take part in it.
        if (_context.Database.CurrentTransaction is not null)
        {
            await EraseInTransactionAsync(personId, deletedAt, cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await EraseInTransactionAsync(personId, deletedAt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task EraseInTransactionAsync(Guid personId, DateTime deletedAt, CancellationToken cancellationToken)
    {
        await _context.Reviews.Where(r => r.CustomerId == personId)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.AuthorName, string.Empty), cancellationToken);
        await _context.ProductQuestions.Where(q => q.AskerId == personId)
            .ExecuteUpdateAsync(x => x.SetProperty(q => q.AskerName, string.Empty), cancellationToken);
        await _context.SavedProducts.Where(s => s.CustomerId == personId).ExecuteDeleteAsync(cancellationToken);
        await _context.ReviewEligibility.Where(e => e.CustomerId == personId).ExecuteDeleteAsync(cancellationToken);
        await _context.ContentReports.Where(r => r.ReporterId == personId).ExecuteDeleteAsync(cancellationToken);
        await _sellers.CloseForDeletedAccountAsync(personId, deletedAt, cancellationToken);
    }
}
