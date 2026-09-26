using Ecommerce.Shared.Insights;
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

/// <summary>
/// The signed-in seller's own products (specs/068, #111): views in the period, ratings, the most viewed. The
/// seller is the token's - there is no id to change into somebody else's (Constitution IV).
/// </summary>
public record GetMyProductInsightsQuery(DateTime? From = null, DateTime? To = null, int Limit = 10) : IRequest<SellerProductInsights>;

public class GetMyProductInsightsQueryValidator : AbstractValidator<GetMyProductInsightsQuery>
{
    public GetMyProductInsightsQueryValidator(InsightsCalendar calendar)
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
        this.ValidPeriod(x => x.From, x => x.To, calendar);
    }
}

public class GetTopViewedQueryValidator : AbstractValidator<GetTopViewedQuery>
{
    public GetTopViewedQueryValidator(InsightsCalendar calendar)
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
        // The same period rule as Order's insights (specs/055, #125).
        this.ValidPeriod(x => x.From, x => x.To, calendar);
    }
}

public class ProductViewHandlers(IProductViewRepository views, IProductRepository products, ICurrentUser currentUser, InsightsCalendar calendar) :
    IRequestHandler<RecordProductViewCommand>,
    IRequestHandler<GetTopViewedQuery, List<ViewedProduct>>,
    IRequestHandler<GetMyProductInsightsQuery, SellerProductInsights>
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

        // Counted on the shop's day (specs/082), the same days the insights read it back by.
        await views.RecordAsync(product.Id, calendar.DayOf(DateTime.UtcNow), cancellationToken);
    }

    public Task<List<ViewedProduct>> Handle(GetTopViewedQuery request, CancellationToken cancellationToken)
    {
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, calendar);
        return views.TopAsync(period.FirstDay, period.LastDay, request.Limit, cancellationToken);
    }

    public Task<SellerProductInsights> Handle(GetMyProductInsightsQuery request, CancellationToken cancellationToken)
    {
        var seller = currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, calendar);
        return views.SellerAsync(seller, period.FirstDay, period.LastDay, request.Limit, cancellationToken);
    }
}
