namespace Ecommerce.Contracts.Identity;

/// <summary>
/// A new shop exists (specs/027).
/// </summary>
/// <remarks>
/// <para>
/// Catalog consumes it and keeps the name, so a product listing can say who sells each thing without
/// asking Identity once per card. A listing of twenty-four products must not become twenty-four
/// lookups, and an anonymous catalogue read must keep working with Identity switched off.
/// </para>
/// <para>
/// No email, no person's name, nothing else about the account: a shopper is shown a shop, and Catalog
/// has no business holding more of somebody's account than the one field it displays.
/// </para>
/// </remarks>
public record SellerRegisteredEvent(
    Guid SellerId,
    string ShopName,
    DateTime RegisteredAt
);

/// <summary>
/// A shop changed its name (specs/027). <b>Nothing about any product changes.</b>
/// </summary>
/// <remarks>
/// That is the whole reason the name is a read model rather than a copy frozen onto each product: a
/// seller renaming their shop should not have to re-save every listing. An order is different and
/// freezes what it bought, because an order is a record of a past event rather than a view of now.
/// </remarks>
public record SellerRenamedEvent(
    Guid SellerId,
    string ShopName,
    DateTime RenamedAt
);
