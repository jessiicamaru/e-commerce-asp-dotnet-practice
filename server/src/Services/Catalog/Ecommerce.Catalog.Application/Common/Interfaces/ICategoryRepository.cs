using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Stages a category for deletion. The caller checks first that nothing is filed under it.</summary>
    void Remove(Category category);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
