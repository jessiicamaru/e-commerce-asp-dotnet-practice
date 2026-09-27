using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Catalog.Application.Sellers;

/// <summary>A shop's description as Identity announced it (#197, specs/099). False for a redelivery or an older one.</summary>
public record RecordShopDescriptionCommand(Guid SellerId, string? Description, DateTime ObservedAt) : IRequest<bool>;

/// <summary>A shop's page (#197, specs/099): anyone; 404 for an unknown, unnamed or suspended seller.</summary>
public record GetShopQuery(Guid SellerId) : IRequest<ShopResponse>;

/// <param name="ProductCount">How many of its products are on the shelf now.</param>
/// <param name="Paused">Its seller is away (specs/107): the page answers and says so, with nothing on the shelf.</param>
public record ShopResponse(Guid SellerId, string ShopName, string? Description, int ProductCount, bool Paused = false);

public class ShopHandlers(ISellerRepository sellers, IProductRepository products) :
    IRequestHandler<RecordShopDescriptionCommand, bool>,
    IRequestHandler<GetShopQuery, ShopResponse>
{
    private readonly ISellerRepository _sellers = sellers;
    private readonly IProductRepository _products = products;

    public Task<bool> Handle(RecordShopDescriptionCommand request, CancellationToken cancellationToken) =>
        _sellers.TryRecordDescriptionAsync(
            request.SellerId, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.ObservedAt, cancellationToken);

    public async Task<ShopResponse> Handle(GetShopQuery request, CancellationToken cancellationToken)
    {
        // One 404 for "no such shop", "not heard of its name yet", "banned" (specs/095) and "closed by staff"
        // (specs/107) - a closed shop is not a page. A paused one is: a shopper following its link learns it is away.
        var seller = await _sellers.GetAsync(request.SellerId, cancellationToken);
        if (seller is null || string.IsNullOrEmpty(seller.ShopName) || seller.Suspended || seller.ClosedAt is not null)
            throw new NotFoundException("Shop not found.");

        var onShelf = await _products.CountOnShelfBySellerAsync(seller.SellerId, cancellationToken);
        return new ShopResponse(seller.SellerId, seller.ShopName, seller.Description, onShelf, seller.PausedAt is not null);
    }
}
