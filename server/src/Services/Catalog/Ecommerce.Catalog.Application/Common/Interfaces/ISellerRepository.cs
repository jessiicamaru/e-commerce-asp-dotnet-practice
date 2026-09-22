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

    /// <summary>The names for these sellers, for building a page of products in one query.</summary>
    Task<Dictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> sellerIds, CancellationToken cancellationToken = default);
}
