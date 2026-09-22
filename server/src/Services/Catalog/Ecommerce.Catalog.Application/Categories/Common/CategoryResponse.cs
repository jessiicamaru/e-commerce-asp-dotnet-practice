namespace Ecommerce.Catalog.Application.Categories.Common;

/// <param name="Language">
/// Which language the name above is in, after the fallback (specs/026) - what tells a client "this
/// category has no English yet". Empty when no language was asked for.
/// </param>
public record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    string Slug,
    Guid? ParentCategoryId,
    bool IsActive,
    string Language = ""
)
{
    /// <summary>
    /// One category as a shopper reads it: the translation if there is one, the category's own text
    /// if there is not - <b>per field</b>, like a product (specs/021).
    /// </summary>
    public static CategoryResponse From(
        Domain.Entities.Category category,
        string language = "",
        string defaultLanguage = "")
    {
        var translation = string.IsNullOrEmpty(language)
            ? null
            : category.Translations.FirstOrDefault(t => t.Language == language);

        return new(
            category.Id,
            translation?.Name ?? category.Name,
            translation?.Description ?? category.Description,
            category.Slug,
            category.ParentCategoryId,
            category.IsActive,
            string.IsNullOrEmpty(language) ? string.Empty : translation is not null ? language : defaultLanguage);
    }
}
