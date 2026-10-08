using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>Product specifications (specs/159): what categories declare and what products hold.</summary>
public interface ISpecificationRepository
{
    /// <summary>One specification of one category, with its translations and its options' (null when not that category's).</summary>
    Task<CategorySpecification?> GetAsync(Guid categoryId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every specification of these categories, with translations and options, in declared order.</summary>
    Task<List<CategorySpecification>> OfCategoriesAsync(IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken = default);

    Task<bool> CodeTakenAsync(Guid categoryId, string code, CancellationToken cancellationToken = default);

    /// <summary>How many products hold a value for this specification.</summary>
    Task<int> ProductsUsingAsync(Guid specificationId, CancellationToken cancellationToken = default);

    /// <summary>How many products hold this option.</summary>
    Task<int> ProductsUsingOptionAsync(Guid optionId, CancellationToken cancellationToken = default);

    Task<List<ProductSpecification>> ValuesOfAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Stages the product's whole set to be replaced by <paramref name="values"/>; the caller saves.</summary>
    Task ReplaceValuesAsync(Guid productId, IReadOnlyList<ProductSpecification> values, CancellationToken cancellationToken = default);

    void Add(CategorySpecification specification);

    void Remove(CategorySpecification specification);

    void RemoveOption(SpecificationOption option);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
