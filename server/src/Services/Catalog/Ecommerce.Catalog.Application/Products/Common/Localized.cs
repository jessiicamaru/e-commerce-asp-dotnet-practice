using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Products.Common;

/// <summary>
/// Picks the text to show, in one place (specs/021).
/// </summary>
/// <remarks>
/// <para>
/// <b>Falling back is per field, not per product.</b> A product translated into Vietnamese with no
/// description shows the Vietnamese name and the original description, rather than reverting both -
/// a half-translated product is the normal state of a shop being translated, and the customer should
/// see every word somebody has got to.
/// </para>
/// <para>
/// <c>Language</c> on the response says which language the NAME came back in, which is how a client
/// can tell "this has no Vietnamese yet" from "this is Vietnamese".
/// </para>
/// </remarks>
public static class Localized
{
    public static string NameOf(Product product, string language) =>
        Translation(product, language)?.Name ?? product.Name;

    public static string? DescriptionOf(Product product, string language) =>
        Translation(product, language)?.Description ?? product.Description;

    /// <summary>Which language the name above is actually in, after the fallback.</summary>
    public static string LanguageOf(Product product, string language, string defaultLanguage) =>
        Translation(product, language) is not null ? language : defaultLanguage;

    /// <summary>
    /// The variant's options in words, in this language: <c>Bộ: Chỉ thân máy</c>. Built from the option
    /// rows rather than from the stored summary, because the stored one is the default language's.
    /// </summary>
    public static string OptionSummaryOf(ProductVariant variant, string language) =>
        variant.Options.Count == 0
            ? string.Empty
            : string.Join(" · ", variant.Options.Select(option =>
            {
                var translated = option.Translations.FirstOrDefault(t => t.Language == language);
                return $"{translated?.Name ?? option.Name}: {translated?.Value ?? option.Value}";
            }));

    public static (string Name, string Value) OptionOf(VariantOption option, string language)
    {
        var translated = option.Translations.FirstOrDefault(t => t.Language == language);
        return (translated?.Name ?? option.Name, translated?.Value ?? option.Value);
    }

    private static ProductTranslation? Translation(Product product, string language) =>
        product.Translations.FirstOrDefault(t => t.Language == language);
}
