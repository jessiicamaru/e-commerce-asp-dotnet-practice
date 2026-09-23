namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// How many times a product page was opened on one day (specs/047). A counter per product per day rather
/// than a row per view: the question is "what do people look at", not "who looked".
/// </summary>
public class ProductView
{
    public Guid ProductId { get; set; }

    public DateOnly Day { get; set; }

    public int Views { get; set; }
}
