using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Application.Products.Saved;

/// <summary>Saves a product for later (specs/075, #109) - the caller's list, from the token.</summary>
public record SaveProductCommand(Guid ProductId) : IRequest;

public record UnsaveProductCommand(Guid ProductId) : IRequest;

public record GetSavedProductsQuery(int Page = 1, int PageSize = 12) : IRequest<PaginatedList<SavedProductResponse>>;

public record GetSavedProductIdsQuery : IRequest<List<Guid>>;

/// <summary>
/// One saved product as the listing reads it now - in the request's language and currency - with when it was
/// saved and whether it can be bought: on the shelf, active, and in stock. A product withdrawn or taken down since
/// stays in the list and says so (research D2) rather than vanishing from something the shopper chose.
/// </summary>
public record SavedProductResponse(ProductResponse Product, DateTime SavedAt, bool Available);

public class GetSavedProductsQueryValidator : AbstractValidator<GetSavedProductsQuery>
{
    public GetSavedProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class SavedProductHandlers(
    ISavedProductRepository saved,
    IProductRepository products,
    ISellerRepository sellers,
    ICurrentUser currentUser,
    IRequestLanguage language,
    IOptions<LanguageOptions> localization,
    IRequestCurrency currency,
    IOptions<CurrencyOptions> money) :
    IRequestHandler<SaveProductCommand>,
    IRequestHandler<UnsaveProductCommand>,
    IRequestHandler<GetSavedProductsQuery, PaginatedList<SavedProductResponse>>,
    IRequestHandler<GetSavedProductIdsQuery, List<Guid>>
{
    private readonly ISavedProductRepository _saved = saved;
    private readonly IProductRepository _products = products;
    private readonly ISellerRepository _sellers = sellers;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task Handle(SaveProductCommand request, CancellationToken cancellationToken)
    {
        // Saving is asking about a PUBLIC product: one that is not on sale is the public lookup's 404, so a hidden
        // product's id is not confirmed to anybody (specs/045).
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !product.IsListed || !product.IsActive)
        {
            throw new NotFoundException("Product not found.");
        }

        await _saved.SaveAsync(Caller(), product.Id, DateTime.UtcNow, cancellationToken);
    }

    public Task Handle(UnsaveProductCommand request, CancellationToken cancellationToken) =>
        _saved.UnsaveAsync(Caller(), request.ProductId, cancellationToken);

    public async Task<PaginatedList<SavedProductResponse>> Handle(GetSavedProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _saved.GetPageAsync(Caller(), request.Page, request.PageSize, cancellationToken);
        var shopNames = await _sellers.GetNamesAsync(
            items.Where(i => i.Product.SellerId is not null).Select(i => i.Product.SellerId!.Value), cancellationToken);

        var responses = items.Select(i => new SavedProductResponse(
                ProductResponse.From(
                    i.Product, language.Current, localization.Value.DefaultLanguage, currency.Current.Code, money.Value.DefaultCurrency,
                    i.Product.SellerId is not null && shopNames.TryGetValue(i.Product.SellerId.Value, out var shop) ? shop : null),
                i.SavedAt,
                i.Product.IsListed && i.Product.IsActive && i.Product.Availability))
            .ToList();

        return new PaginatedList<SavedProductResponse>(responses, total, request.Page, request.PageSize);
    }

    public Task<List<Guid>> Handle(GetSavedProductIdsQuery request, CancellationToken cancellationToken) =>
        _saved.IdsAsync(Caller(), cancellationToken);

    private Guid Caller() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
