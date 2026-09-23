namespace Ecommerce.Inventory.Application.Common.Interfaces;

/// <summary>
/// Who a variant's product belongs to — asked of Catalog, which owns the answer (specs/031).
/// </summary>
/// <remarks>
/// <para>
/// Inventory holds stock, keyed by <b>variant</b> id (specs/020). Ownership lives in
/// <c>products.SellerId</c>, a Catalog column. So the question a seller's stock write raises —
/// "is this yours?" — cannot be answered here, and this is how it leaves.
/// </para>
/// <para>
/// ⚠️ <b>Asked live, never cached.</b> A copy of this seconds out of date refuses a seller their
/// own product with exactly the 404 that means "not yours", and nothing — not the seller, not the
/// log — can tell the two apart. Catalog's read model of shop names and Catalog's read model of
/// availability are both about <i>display</i>, where stale costs a slightly old page; this is an
/// authorization input, where stale costs a refusal that reads as an accusation. specs/031
/// research D2.
/// </para>
/// <para>
/// The interface lives in the Application layer and knows nothing about gRPC, so replacing the
/// transport is one file in Infrastructure.
/// </para>
/// </remarks>
public interface IProductOwnership
{
    /// <summary>
    /// Who owns <paramref name="variantId"/>, or <c>null</c> when no such variant exists.
    /// </summary>
    Task<VariantOwnership?> GetAsync(Guid variantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// One variant's owner. <see cref="SellerId"/> null means <b>the shop itself</b> — every product
/// from before specs/027, and anything an administrator lists. That is a real state, not a missing
/// one: a variant that does not exist is reported by returning <c>null</c> from
/// <see cref="IProductOwnership.GetAsync"/> instead.
/// </summary>
public record VariantOwnership(Guid VariantId, Guid ProductId, Guid? SellerId);
