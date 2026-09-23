using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyBalance;

/// <summary>
/// The caller's money, per currency: on the way, due, paid out (specs/037). No seller id - the seller is
/// the token's subject (Constitution IV).
/// </summary>
public record GetMyBalanceQuery : IRequest<List<BalanceResponse>>;

public class GetMyBalanceQueryHandler(IPayoutRepository payouts, ICurrentUser currentUser)
    : IRequestHandler<GetMyBalanceQuery, List<BalanceResponse>>
{
    private readonly IPayoutRepository _payouts = payouts;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<List<BalanceResponse>> Handle(GetMyBalanceQuery request, CancellationToken cancellationToken)
    {
        var sellerId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        return await _payouts.GetBalanceAsync(sellerId, cancellationToken);
    }
}
