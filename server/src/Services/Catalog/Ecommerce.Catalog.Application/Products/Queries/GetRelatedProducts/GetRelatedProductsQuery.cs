using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Queries.GetRelatedProducts;

/// <summary>
/// Other products like this one (specs/163, #375): on the shelf, from its category and then its department, most reviewed
/// first, never itself. Empty for an unknown or off-shelf product - the same answer, so it says nothing about which ids
/// are real (research D3).
/// </summary>
public record GetRelatedProductsQuery(Guid ProductId, int Limit = 8) : IRequest<List<ProductResponse>>;

public class GetRelatedProductsQueryValidator : AbstractValidator<GetRelatedProductsQuery>
{
    public GetRelatedProductsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 12);
    }
}

public class GetRelatedProductsQueryHandler(
    IProductRepository products,
    ICategoryRepository categories,
    ISellerRepository sellers,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money)
    : IRequestHandler<GetRelatedProductsQuery, List<ProductResponse>>
{
    /// <summary>The listing's own ranking for this (research D2).</summary>
    public const string Ranking = "rating_desc";

    private readonly IProductRepository _products = products;
    private readonly ICategoryRepository _categories = categories;
    private readonly ISellerRepository _sellers = sellers;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;
    private readonly IRequestCurrency _currency = currency;
    private readonly CurrencyOptions _money = money.Value;

    public async Task<List<ProductResponse>> Handle(GetRelatedProductsQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !product.OnShelf)
        {
            return [];
        }

        // The listing, asked twice (research D1): its category first, then - when short - the department around it.
        var related = await ListAsync(product.CategoryId, request.Limit + 1, cancellationToken);
        related.RemoveAll(p => p.Id == product.Id);

        if (related.Count < request.Limit
            && await _categories.GetByIdAsync(product.CategoryId, cancellationToken) is { ParentCategoryId: { } department })
        {
            var taken = related.Select(p => p.Id).Append(product.Id).ToHashSet();
            var more = await ListAsync(department, request.Limit + taken.Count, cancellationToken);
            related.AddRange(more.Where(p => taken.Add(p.Id)));
        }

        related = related.Take(request.Limit).ToList();

        // One query for every shop name, like the listing.
        var shopNames = await _sellers.GetNamesAsync(
            related.Where(p => p.SellerId is not null).Select(p => p.SellerId!.Value),
            cancellationToken);

        return related
            .Select(p => ProductResponse.From(
                p, _language.Current, _localization.DefaultLanguage, _currency.Current.Code, _money.DefaultCurrency,
                p.SellerId is not null && shopNames.TryGetValue(p.SellerId.Value, out var shop) ? shop : null))
            .ToList();
    }

    private async Task<List<Domain.Entities.Product>> ListAsync(Guid categoryId, int size, CancellationToken cancellationToken)
    {
        var (items, _) = await _products.GetPaginatedAsync(
            1, size, categoryId, null, Ranking, cancellationToken,
            _language.Current, _currency.Current.Code, _money.DefaultCurrency);
        return items;
    }
}
