namespace Ecommerce.Contracts.Catalog;

/// <summary>
/// A product and every shape of it have been removed from the catalogue for good (specs/024).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not "stop selling it".</b> Taking something off sale is deactivation, which leaves the
/// row where it is so a shopper's cart and an administrator's report still make sense. This event
/// says the rows are gone, because they should never have existed - test debris, or a mistake.
/// </para>
/// <para>
/// Inventory consumes it to drop the stock rows for those variants. Without that, the count for a
/// product nobody can see stays on the shelf forever, and <c>GET /api/stock/{id}</c> keeps answering
/// for something the catalogue has never heard of.
/// </para>
/// <para>
/// <b>Orders are deliberately not told.</b> An order froze the name, the price, the sku and the
/// option summary of what was bought (specs/009, specs/020, specs/021, specs/022), so it describes
/// the purchase perfectly well after the catalogue forgets the product. That is what freezing is for,
/// and an order that changed because a catalogue row was deleted would be the defect.
/// </para>
/// </remarks>
public record ProductDeletedEvent(
    Guid ProductId,
    IReadOnlyList<Guid> VariantIds,
    DateTime DeletedAt
);
