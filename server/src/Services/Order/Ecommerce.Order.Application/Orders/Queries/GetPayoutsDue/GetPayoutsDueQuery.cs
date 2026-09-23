using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetPayoutsDue;

/// <summary>Staff: every seller with something due now, per currency (specs/037).</summary>
public record GetPayoutsDueQuery : IRequest<List<PayoutDueResponse>>;

public class GetPayoutsDueQueryHandler(IPayoutRepository payouts)
    : IRequestHandler<GetPayoutsDueQuery, List<PayoutDueResponse>>
{
    private readonly IPayoutRepository _payouts = payouts;

    public Task<List<PayoutDueResponse>> Handle(GetPayoutsDueQuery request, CancellationToken cancellationToken) =>
        _payouts.GetDueAsync(cancellationToken);
}
