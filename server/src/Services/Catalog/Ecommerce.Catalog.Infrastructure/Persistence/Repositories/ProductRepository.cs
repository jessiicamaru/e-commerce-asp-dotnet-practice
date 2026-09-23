using Ecommerce.Catalog.Application.Products.Images;
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
            .Include(p => p.Translations)
            .Include(p => p.Variants.OrderBy(v => v.CreatedAt))
                .ThenInclude(v => v.Options)
                    .ThenInclude(o => o.Translations)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Prices)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Product>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var wanted = ids.Distinct().ToList();

        // AsNoTracking: this is a read that answers a question and changes nothing, and it sits on
        // checkout's critical path.
        return await _context.Products
            .AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<int> TrySetImageAsync(
        Guid productId,
        DateTime? expectedUpdatedAt,
        string? contentType,
        DateTime? updatedAt,
        CancellationToken cancellationToken = default) =>
        _context.Products
            .Where(p => p.Id == productId && p.ImageUpdatedAt == expectedUpdatedAt)
            .ExecuteUpdateAsync(set => set
                .SetProperty(p => p.ImageContentType, contentType)
                .SetProperty(p => p.ImageUpdatedAt, updatedAt)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), cancellationToken);

    public async Task<HashSet<string>> GetLiveImageKeysAsync(CancellationToken cancellationToken = default)
    {
        // Two reads rather than one join: a product and a variant produce different key shapes
        // (specs/032), so there is nothing to be gained by fetching them together and a union of
        // two projections says what it means.
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.ImageUpdatedAt != null && p.ImageContentType != null)
            .Select(p => new { p.Id, p.ImageUpdatedAt, p.ImageContentType })
            .ToListAsync(cancellationToken);

        var variants = await _context.ProductVariants
            .AsNoTracking()
            .Where(v => v.ImageUpdatedAt != null && v.ImageContentType != null)
            .Select(v => new { v.Id, v.ImageUpdatedAt, v.ImageContentType })
            .ToListAsync(cancellationToken);

        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in products)
        {
            if (ImageFormat.FromContentType(row.ImageContentType) is { } format)
            {
                keys.Add(ProductImageKey.For(row.Id, row.ImageUpdatedAt!.Value, format));
            }
        }

        foreach (var row in variants)
        {
            if (ImageFormat.FromContentType(row.ImageContentType) is { } format)
            {
                keys.Add(ProductImageKey.ForVariant(row.Id, row.ImageUpdatedAt!.Value, format));
            }
        }

        return keys;
    }

    public Task<int> TrySetVariantImageAsync(
        Guid variantId,
        DateTime? expectedUpdatedAt,
        string? contentType,
        DateTime? updatedAt,
        CancellationToken cancellationToken = default) =>
        _context.ProductVariants
            .Where(v => v.Id == variantId && v.ImageUpdatedAt == expectedUpdatedAt)
            .ExecuteUpdateAsync(set => set
                .SetProperty(v => v.ImageContentType, contentType)
                .SetProperty(v => v.ImageUpdatedAt, updatedAt)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow), cancellationToken);

    public Task<ProductVariant?> GetVariantAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        _context.ProductVariants
            .Include(v => v.Options)
                .ThenInclude(o => o.Translations)
            .Include(v => v.Prices)
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);

    public async Task<List<VariantOwnership>> GetVariantOwnersAsync(
        IEnumerable<Guid> variantIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = variantIds.Distinct().ToList();

        // Three columns, no navigation loaded. An authorization question asked on every stock
        // write should cost a projection, not a graph (specs/031).
        return await _context.ProductVariants
            .AsNoTracking()
            .Where(variant => wanted.Contains(variant.Id))
            .Select(variant => new VariantOwnership(
                variant.Id,
                variant.ProductId,
                variant.Product!.SellerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductVariant>> GetVariantsByIdsAsync(
        IEnumerable<Guid> variantIds,
        CancellationToken cancellationToken = default)
    {
        var wanted = variantIds.Distinct().ToList();

        // AsNoTracking: a read that answers a question and changes nothing, on checkout's critical path.
        return await _context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Options)
                .ThenInclude(o => o.Translations)
            .Include(v => v.Prices)
            .Include(v => v.Product)
                .ThenInclude(p => p!.Translations)
            .Where(v => wanted.Contains(v.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> VariantSkuExistsAsync(string sku, CancellationToken cancellationToken = default) =>
        _context.ProductVariants.AnyAsync(v => v.Sku == sku, cancellationToken);

    public async Task AddVariantAsync(ProductVariant variant, CancellationToken cancellationToken = default) =>
        await _context.ProductVariants.AddAsync(variant, cancellationToken);

    public Task<int> TryRecordVariantAvailabilityAsync(
        Guid variantId,
        bool isAvailable,
        DateTime observedAt,
        CancellationToken cancellationToken = default) =>
        _context.ProductVariants
            .Where(v => v.Id == variantId
                && (v.AvailabilityObservedAt == null || v.AvailabilityObservedAt < observedAt)
                && (v.Availability != isAvailable || v.AvailabilityObservedAt == null))
            .ExecuteUpdateAsync(set => set
                .SetProperty(v => v.Availability, isAvailable)
                .SetProperty(v => v.AvailabilityObservedAt, observedAt)
                .SetProperty(v => v.UpdatedAt, DateTime.UtcNow), cancellationToken);

    public async Task RecomputeProductRollupAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        // One statement: the product's "from" price and its availability are DERIVED, so they are
        // computed where the variants are rather than read into memory and written back.
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE products p SET
                "Price" = COALESCE((SELECT MIN(v."Price") FROM product_variants v
                                    WHERE v."ProductId" = p."Id" AND v."IsActive"), p."Price"),
                "Availability" = COALESCE((SELECT BOOL_OR(v."Availability") FROM product_variants v
                                           WHERE v."ProductId" = p."Id" AND v."IsActive"), false),
                -- The latest thing Inventory said about any of its variants. The column stays useful
                -- for an earlier image, and for anyone asking when this was last true.
                "AvailabilityObservedAt" = (SELECT MAX(v."AvailabilityObservedAt") FROM product_variants v
                                            WHERE v."ProductId" = p."Id" AND v."IsActive"),
                "UpdatedAt" = now()
            WHERE p."Id" = {productId}
            """, cancellationToken);
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
        CancellationToken cancellationToken = default,
        string language = "",
        string currency = "",
        string defaultCurrency = "",
        Guid? sellerId = null)
    {
        // Variants come with the page: the card shows a "from" price and whether the prices differ.
        // Translations too, or every card would fall back to the default language (specs/021), and
        // the price rows, or every card would fall back to the default currency (specs/022).
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Translations)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Prices)
            .AsQueryable();

        if (sellerId.HasValue)
        {
            // "My listings" (specs/027). Indexed, because every seller wants their own page.
            query = query.Where(p => p.SellerId == sellerId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();

            // Diacritics are ignored on BOTH sides, so "may anh" finds "máy ảnh" and "máy ảnh" finds a
            // product somebody typed without accents (specs/021 research D5). No index: unaccent() is
            // not IMMUTABLE, so this is a sequential scan - fine at this size, the first thing to fix
            // at a hundred thousand products.
            // Both sides are unaccented INSIDE the query: unaccent() is PostgreSQL's, so calling it on
            // the term out here would evaluate on the client and throw.
            query = query.Where(p =>
                EF.Functions.Unaccent(p.Name.ToLower()).Contains(EF.Functions.Unaccent(term))
                || p.Sku.ToLower().Contains(term)
                // ...and the requested language's translation, so a Vietnamese shopper finds a product
                // by the Vietnamese name somebody gave it.
                || p.Translations.Any(t => t.Language == language
                    && EF.Functions.Unaccent(t.Name.ToLower()).Contains(EF.Functions.Unaccent(term))));
        }

        // Sorting by price sorts by the price in the currency being ASKED FOR. Sorting by the
        // default currency and labelling the result "cheapest first" would be visibly wrong the moment
        // the two lists are not proportional - which they are not, because an administrator sets each
        // one (specs/022). A product not priced in this currency sorts last: SQL puts NULL last
        // ascending, and `NULLS LAST` is asked for explicitly on the descending sort.
        var inDefaultCurrency = string.IsNullOrEmpty(currency)
            || string.Equals(currency, defaultCurrency, StringComparison.OrdinalIgnoreCase);

        System.Linq.Expressions.Expression<Func<Product, decimal?>> byPrice = inDefaultCurrency
            ? p => p.Price
            : p => p.Variants
                .Where(v => v.IsActive)
                .SelectMany(v => v.Prices)
                .Where(price => price.Currency == currency)
                .Min(price => (decimal?)price.Amount);

        query = sortBy?.ToLower() switch
        {
            "price_asc" => query.OrderBy(byPrice),
            "price_desc" => query.OrderByDescending(byPrice),
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

    public void Remove(Product product)
    {
        // Variants explicitly, because their foreign key is RESTRICT: the schema refuses to let a
        // product quietly take its variants with it, and this says it on purpose. Their options,
        // prices and translations cascade from them, and the product's translations from it.
        _context.ProductVariants.RemoveRange(product.Variants);
        _context.Products.Remove(product);
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

    public Task<int> CountInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _context.Products.CountAsync(p => p.CategoryId == categoryId, cancellationToken);
}
