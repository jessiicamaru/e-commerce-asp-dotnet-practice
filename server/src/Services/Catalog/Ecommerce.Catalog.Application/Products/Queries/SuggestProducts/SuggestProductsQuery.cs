using System.Globalization;
using System.Text;
using Ecommerce.Catalog.Application.Categories.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Queries.SuggestProducts;

/// <summary>What the search box offers while typing (specs/164, #376): a few products and a few categories.</summary>
public record SuggestProductsQuery(string? Q) : IRequest<SearchSuggestions>;

/// <summary>A dropdown's worth - a picture, a name and a price, not a whole product (research D3).</summary>
public record ProductSuggestion(Guid Id, string Name, string? ImageUrl, decimal? Price, string Currency, bool PriceVaries);

public record CategorySuggestion(Guid Id, string Name);

public record SearchSuggestions(List<ProductSuggestion> Products, List<CategorySuggestion> Categories);

public class SuggestProductsQueryValidator : AbstractValidator<SuggestProductsQuery>
{
    public SuggestProductsQueryValidator()
    {
        RuleFor(x => (x.Q ?? string.Empty).Trim().Length)
            .InclusiveBetween(2, 100).WithMessage("Type 2 to 100 characters.")
            .OverridePropertyName("Q");
    }
}

public class SuggestProductsQueryHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money)
    : IRequestHandler<SuggestProductsQuery, SearchSuggestions>
{
    public const int MaxProducts = 6;
    public const int MaxCategories = 4;

    private readonly IProductRepository _products = products;
    private readonly ICategoryRepository _categories = categories;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;
    private readonly IRequestCurrency _currency = currency;
    private readonly CurrencyOptions _money = money.Value;

    public async Task<SearchSuggestions> Handle(SuggestProductsQuery request, CancellationToken cancellationToken)
    {
        var term = request.Q!.Trim();

        // The catalogue's own search (research D1): diacritics ignored on both sides, translation and original, indexed,
        // on the shelf only - the same results page the shopper lands on after Enter, only shorter.
        var (items, _) = await _products.GetPaginatedAsync(
            1, MaxProducts, null, term, null, cancellationToken,
            _language.Current, _currency.Current.Code, _money.DefaultCurrency);

        var found = items
            .Select(p => ProductResponse.From(p, _language.Current, _localization.DefaultLanguage, _currency.Current.Code, _money.DefaultCurrency))
            .Select(p => new ProductSuggestion(p.Id, p.Name, p.ImageUrl, p.Price, p.Currency, p.PriceVaries))
            .ToList();

        // A handful of categories, matched in memory (research D2): the reader's name and the original, folded alike. Every
        // category, as the categories list shows them - `Category.IsActive` is never set and read by nothing (research D6).
        var wanted = Fold(term);
        var matching = (await _categories.GetAllAsync(cancellationToken))
            .Select(c => (Category: c, Shown: CategoryResponse.From(c, _language.Current, _localization.DefaultLanguage)))
            .Where(x => Fold(x.Shown.Name).Contains(wanted, StringComparison.Ordinal)
                || Fold(x.Category.Name).Contains(wanted, StringComparison.Ordinal))
            .OrderBy(x => x.Shown.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxCategories)
            .Select(x => new CategorySuggestion(x.Category.Id, x.Shown.Name))
            .ToList();

        return new SearchSuggestions(found, matching);
    }

    /// <summary>Lower-case, diacritics removed, đ as d - what <c>f_unaccent(lower(...))</c> does for Vietnamese.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var kept = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                kept.Append(c);
            }
        }

        return kept.ToString().Normalize(NormalizationForm.FormC);
    }
}
