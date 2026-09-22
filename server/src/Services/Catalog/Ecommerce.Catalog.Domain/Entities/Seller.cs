namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// What Catalog knows about a shop: its name (specs/027).
/// </summary>
/// <remarks>
/// <para>
/// <b>A read model, not the truth.</b> Identity owns sellers; this is a copy of the one field a
/// product listing displays, kept here so that a page of twenty-four products costs no calls to
/// another service and an anonymous catalogue read keeps working with Identity switched off.
/// </para>
/// <para>
/// It is the same shape as <c>Product.Availability</c>, which is fed by Inventory, and it carries the
/// same warning: <b>seconds behind by design</b>. Nothing may decide anything on it. Showing a shop
/// name is not a decision; refusing a sale would be.
/// </para>
/// <para>
/// No foreign key to anything: the id belongs to a row in another database.
/// </para>
/// </remarks>
public class Seller
{
    /// <summary>The Identity user id. Primary key here; a foreign key to nothing.</summary>
    public Guid SellerId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    /// <summary>
    /// When Identity said so. The guard against an overtaken rename: a late message carrying an
    /// older timestamp must not overwrite a newer name, which is the rule
    /// <c>StockAvailabilityChangedEvent</c> already follows.
    /// </summary>
    public DateTime ObservedAt { get; set; }
}
