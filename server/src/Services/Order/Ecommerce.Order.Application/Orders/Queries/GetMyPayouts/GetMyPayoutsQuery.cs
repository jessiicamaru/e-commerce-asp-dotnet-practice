using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyPayouts;

/// <summary>The payouts made to the caller, newest first (specs/037).</summary>
public record GetMyPayoutsQuery(int Page = 1, int PageSize = 12) : IRequest<PagedResponse<PayoutResponse>>;

public class GetMyPayoutsQueryValidator : AbstractValidator<GetMyPayoutsQuery>
{
    public GetMyPayoutsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetMyPayoutsQueryHandler(IPayoutRepository payouts, ICurrentUser currentUser)
    : IRequestHandler<GetMyPayoutsQuery, PagedResponse<PayoutResponse>>
{
    private readonly IPayoutRepository _payouts = payouts;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PagedResponse<PayoutResponse>> Handle(GetMyPayoutsQuery request, CancellationToken cancellationToken)
    {
        var sellerId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var (items, total) = await _payouts.GetPayoutsPageAsync(sellerId, request.Page, request.PageSize, cancellationToken);
        return new PagedResponse<PayoutResponse>(items, request.Page, request.PageSize, total);
    }
}
