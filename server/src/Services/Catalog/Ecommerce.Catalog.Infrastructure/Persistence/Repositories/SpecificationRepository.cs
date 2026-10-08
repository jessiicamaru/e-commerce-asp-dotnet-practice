using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class SpecificationRepository(CatalogDbContext context) : ISpecificationRepository
{
    private readonly CatalogDbContext _context = context;

    private IQueryable<CategorySpecification> WithText() =>
        _context.CategorySpecifications
            .Include(s => s.Translations)
            .Include(s => s.Options).ThenInclude(o => o.Translations);

    public Task<CategorySpecification?> GetAsync(Guid categoryId, Guid id, CancellationToken cancellationToken = default) =>
        WithText().FirstOrDefaultAsync(s => s.Id == id && s.CategoryId == categoryId, cancellationToken);

    public Task<List<CategorySpecification>> OfCategoriesAsync(
        IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken = default) =>
        WithText()
            .Where(s => categoryIds.Contains(s.CategoryId))
            .OrderBy(s => s.Position).ThenBy(s => s.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<bool> CodeTakenAsync(Guid categoryId, string code, CancellationToken cancellationToken = default) =>
        _context.CategorySpecifications.AnyAsync(s => s.CategoryId == categoryId && s.Code == code, cancellationToken);

    public Task<int> ProductsUsingAsync(Guid specificationId, CancellationToken cancellationToken = default) =>
        _context.ProductSpecifications.CountAsync(v => v.SpecificationId == specificationId, cancellationToken);

    public Task<int> ProductsUsingOptionAsync(Guid optionId, CancellationToken cancellationToken = default) =>
        _context.ProductSpecifications.CountAsync(v => v.OptionId == optionId, cancellationToken);

    public Task<List<ProductSpecification>> ValuesOfAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _context.ProductSpecifications.Where(v => v.ProductId == productId).ToListAsync(cancellationToken);

    public async Task ReplaceValuesAsync(
        Guid productId, IReadOnlyList<ProductSpecification> values, CancellationToken cancellationToken = default)
    {
        var current = await _context.ProductSpecifications.Where(v => v.ProductId == productId).ToListAsync(cancellationToken);
        var wanted = values.ToDictionary(v => v.SpecificationId);

        foreach (var row in current)
        {
            if (!wanted.TryGetValue(row.SpecificationId, out var next))
            {
                _context.ProductSpecifications.Remove(row);
                continue;
            }

            row.OptionId = next.OptionId;
            row.Text = next.Text;
            wanted.Remove(row.SpecificationId);
        }

        _context.ProductSpecifications.AddRange(wanted.Values);
    }

    public void Add(CategorySpecification specification) => _context.CategorySpecifications.Add(specification);

    public void Remove(CategorySpecification specification) => _context.CategorySpecifications.Remove(specification);

    public void RemoveOption(SpecificationOption option) => _context.SpecificationOptions.Remove(option);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
