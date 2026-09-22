using Ecommerce.Catalog.Domain.Entities;

namespace Ecommerce.Catalog.Application.Products.Common;

/// <summary>One shape a product is sold in, as a shopper sees it (specs/020).</summary>
/// <param name="OptionSummary">
/// The options in words - <c>Kit: Body only · Colour: Black</c> - and empty for a product sold in one
/// shape. This is the string frozen onto an order line.
/// </param>
/// <param name="Availability">
/// <c>"InStock"</c> / <c>"OutOfStock"</c> for THIS variant, never a count: a read model fed by
/// Inventory, which nothing may sell against.
/// </param>
/// <param name="Price">
/// What it costs in <paramref name="Currency"/> - and <b>null when it is not sold in that
/// currency</b> (specs/022). Null rather than <c>0</c> on purpose: zero is a price, and a shop that
/// shows a free camera is worse than one that shows a blank.
/// </param>
/// <param name="Currency">Which currency <paramref name="Price"/> is in; empty when none was asked for.</param>
public record VariantResponse(
    Guid Id,
    string Sku,
    decimal? Price,
    string OptionSummary,
    List<VariantOptionResponse> Options,
    string Availability,
    bool IsActive,
    string Currency = ""
)
{
    public static VariantResponse From(
        ProductVariant variant,
        string language = "",
        string currency = "",
        string defaultCurrency = "")
    {
        // Empty language: the stored summary, which is the default language's (specs/021).
        var localise = !string.IsNullOrEmpty(language);

        // Empty currency: the stored price, which is the default currency's - the shape every caller
        // had before specs/022.
        var price = string.IsNullOrEmpty(currency)
            ? variant.Price
            : Priced.Of(variant, currency, defaultCurrency);

        return new(
            variant.Id,
            variant.Sku,
            price,
            localise ? Localized.OptionSummaryOf(variant, language) : variant.OptionSummary,
            variant.Options.Select(option =>
            {
                var (name, value) = localise
                    ? Localized.OptionOf(option, language)
                    : (option.Name, option.Value);

                return new VariantOptionResponse(option.Id, name, value);
            }).ToList(),
            ProductAvailability.From(variant.Availability),
            variant.IsActive,
            currency);
    }
}

/// <param name="Id">
/// Which option this is, so it can be addressed. <b>Without it the translation endpoint added in
/// specs/021 - <c>PUT /api/products/{id}/options/{optionId}/translations/{lang}</c> - cannot be
/// called by anything outside the database</b>, because no response carried the id it needs. Found
/// while writing the camera seeder, which is the first API client that ever tried to use it.
/// </param>
public record VariantOptionResponse(Guid Id, string Name, string Value);
