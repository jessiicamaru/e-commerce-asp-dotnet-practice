using Ecommerce.Catalog.Application.MyData;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>
/// The person's rows in Catalog, as handed out (#217, specs/111). A seller's shop and listings are theirs too; the staff
/// who hid a review or a question are not named.
/// </summary>
public class PersonalDataReader(CatalogDbContext context) : IPersonalDataReader
{
    private readonly CatalogDbContext _context = context;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<object>>> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews.AsNoTracking().Where(r => r.CustomerId == userId).OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.ProductId, r.AuthorName, r.Rating, r.Body, r.CreatedAt, r.UpdatedAt, r.HiddenAt, r.HiddenReason })
            .ToListAsync(cancellationToken);

        var questions = await _context.ProductQuestions.AsNoTracking().Where(q => q.AskerId == userId).OrderBy(q => q.CreatedAt)
            .Select(q => new { q.Id, q.ProductId, q.AskerName, q.Body, q.CreatedAt, q.HiddenAt, q.HiddenReason, q.Answer, q.AnsweredAt })
            .ToListAsync(cancellationToken);

        var saved = await _context.SavedProducts.AsNoTracking().Where(s => s.CustomerId == userId).OrderBy(s => s.SavedAt)
            .Select(s => new { s.ProductId, s.SavedAt })
            .ToListAsync(cancellationToken);

        var reports = await _context.ContentReports.AsNoTracking().Where(r => r.ReporterId == userId).OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, TargetType = r.TargetType.ToString(), r.TargetId, r.ProductId, Reason = r.Reason.ToString(), r.Details,
                Status = r.Status.ToString(), r.CreatedAt, r.ResolvedAt,
            })
            .ToListAsync(cancellationToken);

        var eligibility = await _context.ReviewEligibility.AsNoTracking().Where(e => e.CustomerId == userId)
            .Select(e => new { e.ProductId, e.FirstDeliveredAt })
            .ToListAsync(cancellationToken);

        var shop = await _context.Sellers.AsNoTracking().Where(s => s.SellerId == userId)
            .Select(s => new { s.ShopName, s.Description, s.Suspended, s.PausedAt, s.ClosedAt, s.ClosedReason })
            .ToListAsync(cancellationToken);

        var products = await _context.Products.AsNoTracking().Where(p => p.SellerId == userId).OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Sku, ReviewStatus = p.ReviewStatus.ToString(), p.ReviewReason, p.IsActive })
            .ToListAsync(cancellationToken);

        return new Dictionary<string, IReadOnlyList<object>>
        {
            ["reviews"] = reviews.Cast<object>().ToList(),
            ["questions"] = questions.Cast<object>().ToList(),
            ["savedProducts"] = saved.Cast<object>().ToList(),
            ["reports"] = reports.Cast<object>().ToList(),
            ["reviewEligibility"] = eligibility.Cast<object>().ToList(),
            ["shop"] = shop.Cast<object>().ToList(),
            ["products"] = products.Cast<object>().ToList(),
        };
    }
}
