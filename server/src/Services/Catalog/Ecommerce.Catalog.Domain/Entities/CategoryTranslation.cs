namespace Ecommerce.Catalog.Domain.Entities;

/// <summary>
/// A category's name and description in one language (specs/026).
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="ProductTranslation"/>, and it should have been written at the same
/// time. specs/021 recorded category names as out of scope twice, on the grounds that the feature
/// stayed reviewable - which was true, and which stopped being the whole story the moment the
/// storefront was redesigned: reading the shop in English, every product card said
/// <c>Máy ảnh không gương lật</c> under its name, and the filter offered the same.
/// </para>
/// <para>
/// The category's own <c>Name</c> and <c>Description</c> stay and hold the <b>default-language</b>
/// text, so this is additive and an untranslated category still reads.
/// </para>
/// </remarks>
public class CategoryTranslation
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    /// <summary>A language tag: <c>vi</c>, <c>en</c>. Not an enum - a third language is rows.</summary>
    public string Language { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
