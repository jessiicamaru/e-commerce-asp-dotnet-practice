namespace Ecommerce.Catalog.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>
    /// The CHEAPEST active variant's price - a "from" price (specs/020). Maintained by Catalog whenever
    /// a variant is added, re-priced or deactivated. The column stays because an earlier image reads it.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Whether Inventory last said this product can be bought.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a record of what the owner said, not a fact this service owns.</b> Catalog used to
    /// hold a <c>StockQuantity</c> here, assigned once at creation and never written again, and show
    /// it to anonymous shoppers — a number that could not change, informing a decision to buy. That
    /// is what issue #4 was.
    /// </para>
    /// <para>
    /// Nothing may sell, reserve or charge against this value. Checkout reserves against Inventory's
    /// row under <c>FOR UPDATE</c>, and it must stay that way: a read model fed by messages is
    /// seconds behind by design.
    /// </para>
    /// <para>
    /// Defaults to <c>false</c> on purpose. The two ways to be wrong are not symmetrical — showing
    /// "out of stock" for something in stock costs a sale and self-corrects on the next
    /// announcement; showing "in stock" for something gone takes an order that cannot be filled.
    /// </para>
    /// </remarks>
    public bool Availability { get; set; }

    /// <summary>
    /// When Inventory observed the availability above. <c>null</c> means it has never said anything,
    /// which is what lets the first announcement win the comparison without a special case.
    /// </summary>
    public DateTime? AvailabilityObservedAt { get; set; }

    /// <summary>
    /// Kept after variants (specs/020): it holds the FIRST variant's sku. An earlier Catalog image
    /// selects this column on every query, so dropping it would take that image down rather than
    /// restore it. The sku that matters is the variant's.
    /// </summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>The shapes this product is sold in. At least one; the variant is what is bought.</summary>
    public List<ProductVariant> Variants { get; set; } = [];
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The stored image's type (<c>image/jpeg</c>, <c>image/png</c> or <c>image/webp</c>), decided from
    /// its bytes at upload; <c>null</c> when the product has no image (specs/019).
    /// </summary>
    public string? ImageContentType { get; set; }

    /// <summary>
    /// When the current image was set - also its version, in its address and in its store key, so a
    /// replaced image is never served from a stale cache. Set together with
    /// <see cref="ImageContentType"/> or not at all; the database enforces it.
    /// </summary>
    public DateTime? ImageUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
