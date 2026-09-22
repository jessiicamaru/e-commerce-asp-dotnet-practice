using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Queries.GetProducts;

public class GetProductsQueryHandler(
    IProductRepository productRepository,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money,
    ISellerRepository sellers)
    : IRequestHandler<GetProductsQuery, PaginatedList<ProductResponse>>
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly IRequestLanguage _language = language;
    private readonly LanguageOptions _localization = localization.Value;
    private readonly IRequestCurrency _currency = currency;
    private readonly CurrencyOptions _money = money.Value;
    private readonly ISellerRepository _sellers = sellers;

    public async Task<PaginatedList<ProductResponse>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _productRepository.GetPaginatedAsync(
            request.PageNumber,
            request.PageSize,
            request.CategoryId,
            request.SearchTerm,
            request.SortBy,
            cancellationToken,
            _language.Current,
            _currency.Current.Code,
            _money.DefaultCurrency
        );

        // ONE query for every shop name on the page. FR-003 exists to stop this becoming one call
        // per product - which is what asking Identity per card would have been.
        var shopNames = await _sellers.GetNamesAsync(
            items.Where(p => p.SellerId is not null).Select(p => p.SellerId!.Value),
            cancellationToken);

        var dtos = items
            .Select(p => ProductResponse.From(
                p, _language.Current, _localization.DefaultLanguage, _currency.Current.Code, _money.DefaultCurrency,
                p.SellerId is not null && shopNames.TryGetValue(p.SellerId.Value, out var shop) ? shop : null))
            .ToList();

        return new PaginatedList<ProductResponse>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
