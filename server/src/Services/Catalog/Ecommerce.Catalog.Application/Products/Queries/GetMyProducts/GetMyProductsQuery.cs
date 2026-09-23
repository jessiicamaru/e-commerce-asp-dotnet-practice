using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Queries.GetMyProducts;

/// <summary>
/// The caller's own listings, and only theirs (specs/027).
/// </summary>
/// <remarks>
/// <b>It takes no seller id.</b> Whose listings comes from the token, like everything else about
/// identity in this system — an id in the query string would let one seller read another's page,
/// which is one step from writing to it.
/// </remarks>
public record GetMyProductsQuery : IRequest<PaginatedList<ProductResponse>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public string? SearchTerm { get; init; }
    public string? SortBy { get; init; }
}

public class GetMyProductsQueryHandler(
    IProductRepository products,
    ISellerRepository sellers,
    ICurrentUser currentUser,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money)
    : IRequestHandler<GetMyProductsQuery, PaginatedList<ProductResponse>>
{
    public async Task<PaginatedList<ProductResponse>> Handle(
        GetMyProductsQuery request, CancellationToken cancellationToken)
    {
        var sellerId = currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var (items, totalCount) = await products.GetPaginatedAsync(
            request.PageNumber,
            request.PageSize,
            categoryId: null,
            request.SearchTerm,
            request.SortBy,
            cancellationToken,
            language.Current,
            currency.Current.Code,
            money.Value.DefaultCurrency,
            sellerId);

        // Their own name, once. A seller's page is entirely their own products, so this is one row.
        var shopNames = await sellers.GetNamesAsync([sellerId], cancellationToken);
        shopNames.TryGetValue(sellerId, out var shopName);

        var dtos = items
            .Select(p => ProductResponse.From(
                p,
                language.Current,
                localization.Value.DefaultLanguage,
                currency.Current.Code,
                money.Value.DefaultCurrency,
                shopName))
            .ToList();

        return new PaginatedList<ProductResponse>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
