using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class ProductQuestionRepository(CatalogDbContext context) : IProductQuestionRepository
{
    private readonly CatalogDbContext _context = context;

    public Task<ProductQuestion?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.ProductQuestions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<(List<ProductQuestion> Items, int TotalCount)> GetVisibleAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ProductQuestions.AsNoTracking().Where(q => q.ProductId == productId && q.HiddenAt == null);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(List<QuestionRow> Items, int TotalCount)> GetQueueAsync(
        Guid? sellerId, bool answered, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var joined = from q in _context.ProductQuestions.AsNoTracking()
                     join p in _context.Products.AsNoTracking() on q.ProductId equals p.Id
                     where p.SellerId == sellerId && q.HiddenAt == null && (q.AnsweredAt != null) == answered
                     select new { q, p.Name };
        var total = await joined.CountAsync(cancellationToken);
        var ordered = answered
            ? joined.OrderByDescending(x => x.q.AnsweredAt).ThenBy(x => x.q.Id)
            : joined.OrderBy(x => x.q.CreatedAt).ThenBy(x => x.q.Id);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new QuestionRow(x.q, x.Name)).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(List<QuestionRow> Items, int TotalCount)> GetForStaffAsync(
        bool hidden, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ProductQuestions.AsNoTracking()
            .Where(q => hidden ? q.HiddenAt != null || q.AnswerHiddenAt != null : q.HiddenAt == null);
        var total = await query.CountAsync(cancellationToken);
        var joined = from q in query
                     join p in _context.Products.AsNoTracking() on q.ProductId equals p.Id
                     select new { q, p.Name };
        var items = await joined.OrderByDescending(x => x.q.CreatedAt).ThenBy(x => x.q.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new QuestionRow(x.q, x.Name)).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(ProductQuestion question, CancellationToken cancellationToken = default) =>
        await _context.ProductQuestions.AddAsync(question, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);

    public Task<int> TryAnswerFirstAsync(Guid id, string answer, Guid by, DateTime now,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(stage, ct => _context.ProductQuestions
            .Where(q => q.Id == id && q.AnsweredAt == null && q.HiddenAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.Answer, answer)
                .SetProperty(q => q.AnsweredBy, by)
                .SetProperty(q => q.AnsweredAt, now)
                .SetProperty(q => q.AnswerUpdatedAt, now), ct), cancellationToken);

    public Task<int> TryRewriteAsync(Guid id, string answer, Guid by, DateTime now,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(stage, ct => _context.ProductQuestions
            .Where(q => q.Id == id && q.AnsweredAt != null && q.AnswerHiddenAt == null && q.HiddenAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.Answer, answer)
                .SetProperty(q => q.AnsweredBy, by)
                .SetProperty(q => q.AnswerUpdatedAt, now), ct), cancellationToken);

    public Task<int> TryHideAsync(Guid id, string reason, Guid by, DateTime now,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(stage, ct => _context.ProductQuestions
            .Where(q => q.Id == id && q.HiddenAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.HiddenAt, now)
                .SetProperty(q => q.HiddenReason, reason)
                .SetProperty(q => q.HiddenBy, by), ct), cancellationToken);

    public Task<int> TryRestoreAsync(Guid id, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(stage, ct => _context.ProductQuestions
            .Where(q => q.Id == id && q.HiddenAt != null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.HiddenAt, (DateTime?)null)
                .SetProperty(q => q.HiddenReason, (string?)null)
                .SetProperty(q => q.HiddenBy, (Guid?)null), ct), cancellationToken);

    public Task<int> TryHideAnswerAsync(Guid id, string reason, Guid by, DateTime now,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(stage, ct => _context.ProductQuestions
            .Where(q => q.Id == id && q.AnsweredAt != null && q.AnswerHiddenAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.AnswerHiddenAt, now)
                .SetProperty(q => q.AnswerHiddenReason, reason)
                .SetProperty(q => q.AnswerHiddenBy, by), ct), cancellationToken);

    public Task<int> TryRestoreAnswerAsync(Guid id, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default) =>
        GuardedAsync(stage, ct => _context.ProductQuestions
            .Where(q => q.Id == id && q.AnswerHiddenAt != null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.AnswerHiddenAt, (DateTime?)null)
                .SetProperty(q => q.AnswerHiddenReason, (string?)null)
                .SetProperty(q => q.AnswerHiddenBy, (Guid?)null), ct), cancellationToken);

    /// <summary>
    /// The one statement decides; only if it changed a row is <paramref name="stage"/> run and saved - in its
    /// transaction, inside the execution strategy because Catalog retries on failure.
    /// </summary>
    private Task<int> GuardedAsync(Func<CancellationToken, Task> stage, Func<CancellationToken, Task<int>> statement,
        CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var rows = await statement(cancellationToken);
            if (rows == 1)
            {
                await stage(cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return rows;
        });
    }
}
