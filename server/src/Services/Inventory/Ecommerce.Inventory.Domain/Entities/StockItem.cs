namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// What the inventory knows about one product. Available quantity is derived rather than stored:
/// a third number would be free to disagree with the other two.
/// </summary>
public class StockItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// The sellable unit this stock belongs to - <b>a variant id</b> since specs/020, though the column
    /// is still called <c>ProductId</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Not renamed on purpose: renaming a column is a breaking schema change, and an earlier
    /// Inventory image selects this one. Inventory does not know what a variant is - it counts
    /// sellable units by id - and for every product that existed before variants, its id IS its only
    /// variant's id, so every row here was already correct (specs/020 research D2, D9).
    /// </remarks>
    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
