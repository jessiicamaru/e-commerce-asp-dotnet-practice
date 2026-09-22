namespace Ecommerce.Contracts.Catalog;

/// <summary>
/// Another shape of a product now exists, and it can be stocked (specs/020).
/// </summary>
/// <remarks>
/// <para>
/// A variant is the sellable unit: Inventory counts stock per variant, so it has to hear about each
/// one. The FIRST variant of a product does not need this event - its id is the product's own, which
/// <see cref="ProductCreatedEvent"/> already announced - so this is published only for the ones added
/// afterwards.
/// </para>
/// <para>
/// No price: what a thing costs is Catalog's, and Inventory has no use for it.
/// </para>
/// </remarks>
public record ProductVariantCreatedEvent(
    Guid ProductId,
    Guid VariantId,
    string Sku,
    DateTime CreatedAt
);
