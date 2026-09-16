namespace Ecommerce.Inventory.Domain.Entities;

/// <summary>
/// What the inventory knows about one product. Available quantity is derived rather than stored:
/// a third number would be free to disagree with the other two.
/// </summary>
public class StockItem
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
