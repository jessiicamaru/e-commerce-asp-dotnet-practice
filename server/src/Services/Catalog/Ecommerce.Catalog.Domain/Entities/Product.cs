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

    /// <summary>
    /// This product's name and description in other languages (specs/021). <see cref="Name"/> and
    /// <see cref="Description"/> hold the default-language text and are the fallback.
    /// </summary>
    public List<ProductTranslation> Translations { get; set; } = [];
    /// <summary>
    /// Who sells this, or <c>null</c> for <b>the shop itself</b> (specs/027) - which is every product
    /// listed before sellers existed, and anything an administrator lists.
    /// </summary>
    /// <remarks>
    /// An Identity user id, and a foreign key to nothing: it names a row in another database.
    /// </remarks>
    public Guid? SellerId { get; set; }

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
    /// <summary>
    /// Whether the catalogue may show and sell it (specs/045). A seller's new product waits for a
    /// moderator; the shop's own are approved as listed. Stored as text, and every product from before
    /// this reads Approved.
    /// </summary>
    public ProductReviewStatus ReviewStatus { get; set; } = ProductReviewStatus.Approved;

    /// <summary>Why it was rejected or taken down - the seller reads this.</summary>
    public string? ReviewReason { get; set; }

    /// <summary>When it last went into the queue: listed, resubmitted, or edited after approval.</summary>
    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    /// <summary>On the shelf: approved. Everything public and everything sellable asks this.</summary>
    public bool IsListed => ReviewStatus == ProductReviewStatus.Approved;

    /// <summary>
    /// The average of the visible reviews and how many there are (specs/046) - kept on the row, recomputed
    /// in the transaction of every review change, so a listing shows stars without counting anything.
    /// </summary>
    public decimal? RatingAverage { get; set; }

    public int RatingCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Where a product stands with the moderators (specs/045).</summary>
public enum ProductReviewStatus
{
    Approved,
    Pending,
    Rejected
}
