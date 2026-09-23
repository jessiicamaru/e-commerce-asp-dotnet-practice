using Ecommerce.Catalog.Application.Common;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Shared.Authentication;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Products.Views;

/// <summary>
/// A product page was opened (specs/047). Counted only when it is on the shelf and the viewer is a
/// shopper - not its seller checking their own listing, not staff reviewing it - and quiet either way,
/// so the answer says nothing about the product.
/// </summary>
public record RecordProductViewCommand(Guid ProductId) : IRequest;

/// <summary>What people look at most, for administrators.</summary>
public record GetTopViewedQuery(DateTime? From = null, DateTime? To = null, int Limit = 10) : IRequest<List<ViewedProduct>>;

public class GetTopViewedQueryValidator : AbstractValidator<GetTopViewedQuery>
{
    public GetTopViewedQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From < x.To).WithMessage("The period must start before it ends.");
    }
}

public class ProductViewHandlers(IProductViewRepository views, IProductRepository products, ICurrentUser currentUser) :
    IRequestHandler<RecordProductViewCommand>,
    IRequestHandler<GetTopViewedQuery, List<ViewedProduct>>
{
    public async Task Handle(RecordProductViewCommand request, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !product.IsListed
            || currentUser.IsInRole(StaffRoles.Admin) || currentUser.IsInRole(StaffRoles.Moderator)
            || (product.SellerId is not null && product.SellerId == currentUser.Id))
        {
            return;
        }

        await views.RecordAsync(product.Id, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
    }

    public Task<List<ViewedProduct>> Handle(GetTopViewedQuery request, CancellationToken cancellationToken)
    {
        var to = request.To ?? DateTime.UtcNow;
        var from = request.From ?? to.AddDays(-30);
        return views.TopAsync(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), request.Limit, cancellationToken);
    }
}
