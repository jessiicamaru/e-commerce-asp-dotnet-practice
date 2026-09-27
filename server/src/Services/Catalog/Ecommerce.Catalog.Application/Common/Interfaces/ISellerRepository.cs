using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Common.Interfaces;

/// <summary>
/// The shop names Catalog keeps so a listing costs no call to another service (specs/027).
/// </summary>
public interface ISellerRepository
{
    /// <summary>
    /// Records a shop name if what is stored is older, and reports whether anything changed.
    /// </summary>
    /// <remarks>
    /// <b>Zero is a normal answer</b>, not a failure: a redelivered message, or one overtaken by a
    /// newer rename. Do not "simplify" this into writing whenever the name differs - that survives a
    /// duplicate and loses to a late message, which is the trap
    /// <c>ProductRepository.TryRecordAvailabilityAsync</c> already documents.
    /// </remarks>
    Task<bool> TryRecordAsync(Guid sellerId, string shopName, DateTime observedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a seller's shop as closed or open if nothing newer is stored, and - only then - copies it onto every one of
    /// the seller's products, in one transaction (#193, specs/095). A seller not yet known is created with an empty name
    /// that the registration fills in. Returns whether anything changed.
    /// </summary>
    Task<bool> TryRecordSuspensionAsync(Guid sellerId, bool suspended, DateTime changedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a shop's description if nothing newer is stored (#197, specs/099); a seller not known yet is created with an
    /// empty name the registration fills in. Returns whether anything changed.
    /// </summary>
    Task<bool> TryRecordDescriptionAsync(Guid sellerId, string? description, DateTime observedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses, resumes, closes or reopens a shop with one guarded <c>UPDATE</c> (#214, specs/107), recomputes its
    /// products' shelf flag from every reason, runs <paramref name="stage"/> (audit, notices) and saves - one
    /// transaction. False when the guard matched nothing; the caller reads the row to say why.
    /// </summary>
    Task<bool> TryMoveShopAsync(
        Guid sellerId, ShopMove move, DateTime at, string? reason, Guid? by,
        Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    /// <summary>Shops staff closed, newest closure first (specs/107).</summary>
    Task<(List<Seller> Items, int Total)> GetClosedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<Seller?> GetAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>The names for these sellers, for building a page of products in one query.</summary>
    Task<Dictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> sellerIds, CancellationToken cancellationToken = default);
}

/// <summary>A change of a shop's state (specs/107): the seller's pause and resume, staff's close and reopen.</summary>
public enum ShopMove { Pause, Resume, Close, Reopen }
