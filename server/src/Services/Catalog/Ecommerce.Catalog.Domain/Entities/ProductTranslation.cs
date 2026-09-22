namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// A product's name and description in one language (specs/021).
/// </summary>
/// <remarks>
/// <para>
/// The product's own <c>Name</c> and <c>Description</c> stay and hold the <b>default-language</b>
/// text - what an administrator typed when there was only one language. They are the fallback when a
/// translation is missing, and what an earlier image reads.
/// </para>
/// <para>
/// A row per language rather than a column per language: a third language is then rows and a
/// translation file, not a migration.
/// </para>
/// </remarks>
public class ProductTranslation
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>A language tag: <c>vi</c>, <c>en</c>. Not an enum, for the reason above.</summary>
    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>
/// What a customer chooses between, in one language: <c>Kit</c> → <c>Bộ</c>, <c>Body only</c> →
/// <c>Chỉ thân máy</c> (specs/021).
/// </summary>
/// <remarks>
/// Option values are read by customers as much as product names are. A shop whose names are Vietnamese
/// and whose colours are English is not translated.
/// </remarks>
public class VariantOptionTranslation
{
    public Guid Id { get; set; }

    public Guid OptionId { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
