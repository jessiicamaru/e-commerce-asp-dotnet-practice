using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Catalog.Infrastructure.Persistence.Repositories;

public class CategoryRepository(CatalogDbContext context) : ICategoryRepository
{
    private readonly CatalogDbContext _context = context;

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // With its translations: this is the read the write path uses, and a handler that upserts
        // one has to see the ones already there or it inserts a second row per language.
        return await _context.Categories
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Categories.FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
    }

    public async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Translations come with the list, or every category on every page falls back to its
        // default text (specs/026) - which is exactly what the storefront was showing.
        return await _context.Categories
            .Include(c => c.Translations)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
    }

    public void Remove(Category category) => _context.Categories.Remove(category);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
