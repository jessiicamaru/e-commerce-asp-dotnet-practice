namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// One sellable shape of a product: a camera body alone, or the kit with a lens, in black or in
/// silver (specs/020). <b>The variant is what is priced, stocked, added to a cart and bought</b> - the
/// product is what a shopper recognises.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>For every product that existed before this feature, the variant's <see cref="Id"/> IS the
/// product's id.</b> That is deliberate: Inventory's stock rows, Cart's lines and Order's lines all
/// held a product id, in three other databases, and reusing the id made every one of them correct with
/// no cross-service backfill. For variants created afterwards the two ids are unrelated. <b>Nothing may
/// assume either way</b> - see specs/020 research D2.
/// </para>
/// <para>
/// A variant is deactivated, never deleted: orders refer to it, and an order must keep describing what
/// was bought.
/// </para>
/// </remarks>
public class ProductVariant
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Unique across the catalogue. Frozen onto an order line, so it never changes.</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>What this shape costs. The product's own <c>Price</c> is the cheapest of these.</summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The options flattened for display and for freezing: <c>Kit: Body only · Colour: Black</c>.
    /// Empty for a product sold in only one shape - there is nothing to choose between.
    /// </summary>
    public string OptionSummary { get; set; } = string.Empty;

    public List<VariantOption> Options { get; set; } = [];

    /// <summary>
    /// What this shape costs in each currency (specs/022). A currency with no row here is one this
    /// variant is <b>not sold in</b> - never one to convert into.
    /// </summary>
    public List<VariantPrice> Prices { get; set; } = [];

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether Inventory last said this variant can be bought - a read model, per variant.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c> for the same asymmetry as the product's flag: showing "out of stock"
    /// for something in stock costs a sale and self-corrects on the next announcement, while showing
    /// "in stock" for something gone takes an order that cannot be filled. <b>Nothing may sell against
    /// it</b>: checkout reserves against Inventory's row under <c>FOR UPDATE</c>.
    /// </remarks>
    public bool Availability { get; set; }

    /// <summary>When Inventory observed it. Null means it has never said anything.</summary>
    public DateTime? AvailabilityObservedAt { get; set; }

    /// <summary>
    /// This shape's own photograph type, decided from its bytes at upload; <c>null</c> when the
    /// variant has none, which is the normal case (specs/032).
    /// </summary>
    /// <remarks>
    /// A variant is the exact thing being sold, so a variant is what a photograph is of. Attaching
    /// it to an option VALUE instead was rejected on measured data: Fujifilm X-T5 has two black
    /// variants that do not look alike, because one has a lens mounted.
    /// </remarks>
    public string? ImageContentType { get; set; }

    /// <summary>
    /// When this shape's photograph was set - also its version, in its address and in its store key.
    /// Set together with <see cref="ImageContentType"/> or not at all; the database enforces it.
    /// </summary>
    public DateTime? ImageUpdatedAt { get; set; }

    /// <summary>
    /// The unguessable part of the image's address (specs/081, #166), new with every image. A product that is not
    /// on sale serves its image only to an address carrying it - an address only its seller and staff are given, in
    /// the responses they may read - so knowing the id no longer shows the photograph of something off the shelf.
    /// </summary>
    public Guid? ImageAccessKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Can a customer buy this right now - both halves of the question.</summary>
    /// <remarks>An unapproved product sells nothing, whatever its variants say (specs/045).</remarks>
    public bool Sellable => IsActive && (Product is null || Product.OnShelf);

    /// <summary>
    /// Flattens options the one way, so the summary shown in a cart and the summary frozen on an order
    /// line cannot drift apart.
    /// </summary>
    /// <remarks>
    /// <b>Ordered by name, because nothing else orders them.</b> The rows come back in whatever order
    /// the database yields, so the same two options could be summarised as <c>Kit: … · Colour: …</c>
    /// once and <c>Colour: … · Kit: …</c> the next time - which is two different strings for one
    /// variant, and this method exists precisely to stop that. Seen in the seeded catalogue, where two
    /// shapes of the same camera listed their options in opposite orders.
    /// </remarks>
    public static string Summarise(IEnumerable<VariantOption> options) =>
        string.Join(" · ", options
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .Select(option => $"{option.Name}: {option.Value}"));
}

/// <summary>
/// One thing a customer chooses between: a name and a value, both free text (specs/020).
/// </summary>
/// <remarks>
/// Free text on purpose: a camera has kits, a shirt has sizes, a phone has storage, and a schema that
/// names them can only hold the shapes somebody thought of first. The cost, recorded rather than
/// solved: <c>"Black"</c> and <c>"black"</c> are two values until somebody normalises them.
/// </remarks>
public class VariantOption
{
    public Guid Id { get; set; }

    public Guid VariantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    /// <summary>This option in other languages (specs/021); the columns above are the fallback.</summary>
    public List<VariantOptionTranslation> Translations { get; set; } = [];
}
