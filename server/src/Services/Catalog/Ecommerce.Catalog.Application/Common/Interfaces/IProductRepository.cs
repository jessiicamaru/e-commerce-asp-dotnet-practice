using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

public interface IProductRepository : ILiveImageKeys
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Loads several products by id, for the pricing call that answers a whole order at once.
    /// </summary>
    /// <remarks>
    /// Returns only the ones that exist, so the caller compares counts and decides. That comparison
    /// is what makes "refuse the order in full" structural: there is no point at which four of five
    /// lines are priced and the fifth is not.
    /// <para>
    /// No <c>Include</c> of the category - the pricing call needs a name and a number, and dragging
    /// a navigation property into a hot path for nobody's benefit is how a lookup on checkout's
    /// critical path stops being invisible.
    /// </para>
    /// </remarks>
    Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <param name="language">
    /// Which language's translations to search as well as the default text (specs/021). Empty searches
    /// the default text only, which is what a caller with no request does.
    /// </param>
    Task<(List<Product> Items, int TotalCount)> GetPaginatedAsync(int pageNumber, int pageSize, Guid? categoryId, string? searchTerm, string? sortBy, CancellationToken cancellationToken = default, string language = "", string currency = "", string defaultCurrency = "", Guid? sellerId = null, bool listedOnly = true);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>The moderators' queue or history for one review status (specs/045), with what a card shows.</summary>
    Task<(List<Product> Items, int TotalCount)> GetForReviewAsync(
        ProductReviewStatus status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a product between review states with a guarded <c>UPDATE ... WHERE "ReviewStatus" IN (from)</c>,
    /// in one transaction with what <paramref name="stage"/> writes (the audit entry, the notice). Only the
    /// winner's stage runs. Returns whether this call moved it.
    /// </summary>
    Task<bool> TryReviewAsync(
        Guid productId,
        IReadOnlyCollection<ProductReviewStatus> from,
        ProductReviewStatus to,
        string? reason,
        Guid? reviewedBy,
        DateTime at,
        Func<CancellationToken, Task> stage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a product and every shape of it for deletion (specs/024).
    /// </summary>
    /// <remarks>
    /// The variants go first <b>explicitly</b>: the foreign key from a variant to its product is
    /// <c>RESTRICT</c>, not cascade, precisely so a product cannot take its variants down by
    /// accident - an order refers to a variant. Deleting them here is saying it on purpose.
    /// </remarks>
    void Remove(Product product);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records what Inventory last said about a product's availability, and returns the number of
    /// rows that changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zero is a normal answer.</b> It means the announcement was a duplicate, or an older
    /// observation that a newer one has already overtaken, or about a product this catalogue does
    /// not hold. None of those is an error.
    /// </para>
    /// <para>
    /// Both conditions live in the statement the database executes. The duplicate case is the easy
    /// half; the half that costs something is an <i>older</i> announcement arriving <i>later</i>,
    /// which without the comparison would overwrite a newer one and leave the listing offering goods
    /// that are gone. The broker preserves order only in the absence of redelivery, and redelivery
    /// is normal operation.
    /// </para>
    /// </remarks>
    Task<int> TryRecordAvailabilityAsync(
        Guid productId,
        bool isAvailable,
        DateTime observedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this catalogue holds the product at all. Used only to explain a zero-row result: a
    /// product that exists was already current, one that does not is an announcement for somebody
    /// else's database. The two deserve different log levels.
    /// </summary>
    /// <summary>
    /// Points the product at a new image - or at none, with both values <c>null</c> - but only if its
    /// image is still the one the caller saw (<paramref name="expectedUpdatedAt"/>). Returns the rows
    /// changed: <c>0</c> means someone else changed it first (specs/019 research D3).
    /// </summary>
    /// <remarks>
    /// One statement, so the comparison and the write cannot be separated by another writer. Without
    /// the guard two concurrent replacements could each delete the file the other had just installed.
    /// </remarks>
    Task<int> TrySetImageAsync(
        Guid productId,
        DateTime? expectedUpdatedAt,
        string? contentType,
        DateTime? updatedAt,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>How many products are filed under a category - what makes deleting it refusable.</summary>
    Task<int> CountInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    // ---- Variants (specs/020). The variant is the sellable unit: it carries the sku and the price.

    /// <summary>One variant with its options and its product, or <c>null</c>.</summary>
    Task<ProductVariant?> GetVariantAsync(Guid variantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches one variant's image, only if nobody switched it since <paramref name="expectedUpdatedAt"/>
    /// was read (specs/032). Returns the rows affected - zero means somebody else got there first.
    /// </summary>
    Task<int> TrySetVariantImageAsync(
        Guid variantId,
        DateTime? expectedUpdatedAt,
        string? contentType,
        DateTime? updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Several variants at once, with their options and products - the pricing call answers a whole
    /// order in one query. Returns only those that exist, so the caller compares counts and decides.
    /// </summary>
    Task<List<ProductVariant>> GetVariantsByIdsAsync(IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default);

    /// <summary>Whether any variant already uses this sku. Skus are unique across the catalogue.</summary>
    /// <summary>
    /// Who owns the products these variants belong to (specs/031).
    /// </summary>
    /// <remarks>
    /// A projection rather than a reuse of <see cref="GetVariantsByIdsAsync"/>, which loads options
    /// and their translations: an authorization question should not drag a display graph behind it.
    /// A variant that does not exist is simply absent from the result - the caller turns absence
    /// into its own refusal, which is what keeps "not yours" and "no such variant" identical.
    /// </remarks>
    Task<List<VariantOwnership>> GetVariantOwnersAsync(
        IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default);

    Task<bool> VariantSkuExistsAsync(string sku, CancellationToken cancellationToken = default);

    Task AddVariantAsync(ProductVariant variant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records what Inventory last said about a VARIANT, guarded exactly as the product-level version
    /// is: a duplicate or an overtaken announcement changes zero rows, and that is a normal answer.
    /// </summary>
    Task<int> TryRecordVariantAvailabilityAsync(
        Guid variantId,
        bool isAvailable,
        DateTime observedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a product's own columns back in line with its variants: <c>Price</c> becomes the cheapest
    /// active variant's, and <c>Availability</c> becomes "any active variant is available". Both columns
    /// stay because an earlier image reads them (specs/020 research D3).
    /// </summary>
    /// <returns>
    /// Whether the product came back in stock: its rollup went from unavailable to available in this statement
    /// (specs/075) - what a saved product's back-in-stock notice is about.
    /// </returns>
    Task<bool> RecomputeProductRollupAsync(Guid productId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Who a variant belongs to. <c>SellerId</c> null means the shop itself (specs/027).
/// </summary>
/// <remarks>
/// NOT <c>VariantOwner</c>: the generated proto message is called that, and two types with the
/// same name in two namespaces compile right up until one file needs both - which the gRPC service
/// does. The same trap the front end documents for a service class and its model type.
/// </remarks>
public record VariantOwnership(Guid VariantId, Guid ProductId, Guid? SellerId);

/// <summary>
/// Every store key a product or a variant currently names (specs/033).
/// </summary>
/// <remarks>
/// <para>
/// A one-method interface rather than a method on the repository, because the orphan scan needs
/// exactly this and nothing else - and because a test for "the catalogue could not be read" should
/// not have to implement twenty-one members to say so. <c>IProductRepository</c> implements it.
/// </para>
/// <para>
/// ⚠️ <b>A failure here must NOT be read as "there are none".</b> An empty set makes every file in
/// the store an orphan, and the caller deletes them. It throws rather than returning empty, and the
/// caller lets it.
/// </para>
/// </remarks>
public interface ILiveImageKeys
{
    Task<HashSet<string>> GetLiveImageKeysAsync(CancellationToken cancellationToken = default);
}
