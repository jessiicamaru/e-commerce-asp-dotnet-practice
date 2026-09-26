namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// Somebody who has already opened a product page today (specs/086, #173), so opening it again adds no view.
/// </summary>
/// <remarks>
/// <see cref="Viewer"/> is a SHA-256 of who it was - a signed-in person's id, or the random id the storefront keeps
/// for a visitor - never the id itself. Only today's rows matter: a product's first view on a day deletes its rows
/// from earlier days in the same statement, so the table holds at most a day of viewers per product.
/// </remarks>
public class ProductViewer
{
    public Guid ProductId { get; set; }

    public DateOnly Day { get; set; }

    public string Viewer { get; set; } = string.Empty;
}
