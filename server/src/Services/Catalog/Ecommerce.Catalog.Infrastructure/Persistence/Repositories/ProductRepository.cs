using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class ProductRepository(CatalogDbContext context) : IProductRepository
{
    private readonly CatalogDbContext _context = context;

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Include(p => p.Category)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<Product> Items, int TotalCount)> GetPaginatedAsync(
        int pageNumber,
        int pageSize,
        Guid? categoryId,
        string? searchTerm,
        string? sortBy,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.Include(p => p.Category).AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        query = sortBy?.ToLower() switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "name_desc" => query.OrderByDescending(p => p.Name),
            _ => query.OrderBy(p => p.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One statement:
    /// <code>
    /// UPDATE products
    ///    SET "Availability" = @isAvailable, "AvailabilityObservedAt" = @observedAt
    ///  WHERE "Id" = @productId
    ///    AND ("AvailabilityObservedAt" IS NULL OR "AvailabilityObservedAt" &lt; @observedAt)
    /// </code>
    /// Do not "simplify" this by dropping the timestamp comparison and only writing when the value
    /// differs. That survives a duplicate and fails an overtaken announcement — an older
    /// "out of stock" landing after a newer "in stock" would win, and the listing would offer goods
    /// that are gone. The two cases look like one requirement and are not.
    /// </summary>
    public async Task<int> TryRecordAvailabilityAsync(
        Guid productId,
        bool isAvailable,
        DateTime observedAt,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.Id == productId
                && (p.AvailabilityObservedAt == null || p.AvailabilityObservedAt < observedAt))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Availability, isAvailable)
                    .SetProperty(p => p.AvailabilityObservedAt, observedAt)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await _context.Products.AnyAsync(p => p.Id == productId, cancellationToken);
    }
}
