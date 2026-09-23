namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// What somebody who received a product thought of it (specs/046): one per customer per product, editable
/// by its author, hidden - not deleted - by a moderator.
/// </summary>
public class Review
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Their first name when they wrote it, from the token - never from the request.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>1 to 5.</summary>
    public int Rating { get; set; }

    public string? Body { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? HiddenAt { get; set; }

    public string? HiddenReason { get; set; }

    public Guid? HiddenBy { get; set; }
}

/// <summary>
/// Somebody received this product (specs/046) - fed by Order's <c>ParcelDeliveredEvent</c>, because Catalog
/// does not know who bought what. The one thing that lets a review be written.
/// </summary>
public class ReviewEligibility
{
    public Guid ProductId { get; set; }

    public Guid CustomerId { get; set; }

    public DateTime FirstDeliveredAt { get; set; }
}
