namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// What one variant costs in one currency (specs/022). An amount somebody <b>decided</b>, never one
/// a rate produced.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>The absence of a row is the meaning.</b> No row for a currency means the variant is not sold
/// in it - it does <b>not</b> mean "use the other currency's amount". This is the one place where the
/// design deliberately differs from the translations beside it (specs/021): a missing translation
/// falls back and the worst case is a shopper reading English, while a missing price falling back
/// would sell a 40,000,000₫ camera for 1,600₫ or charge $40,000,000 for it.
/// </para>
/// <para>
/// The price hangs off the <b>variant</b>, not the product, because the variant is the sellable unit
/// (specs/020): a body and a kit are different prices in both currencies, independently, and no rule
/// divides one product price between them.
/// </para>
/// <para>
/// <see cref="ProductVariant.Price"/> stays and holds the <b>default currency's</b> amount - what an
/// image built before this feature reads, and the fallback for that one currency only.
/// </para>
/// </remarks>
public class VariantPrice
{
    public Guid Id { get; set; }

    public Guid VariantId { get; set; }

    /// <summary>An ISO 4217 code, upper case. Not an enum: a third currency is rows, not a migration.</summary>
    public string Currency { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
