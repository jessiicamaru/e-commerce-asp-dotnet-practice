using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetOrderForStaff;

/// <summary>
/// Staff: any order's detail - its lines, where it goes and every parcel (specs/038). Staff ship the
/// shop's parcel, and cannot ship what they cannot see; `GetMyOrderById` answers the owner only.
/// </summary>
/// <remarks>
/// No owner in the query, deliberately: the <c>Admin</c> role on the route is the whole permission.
/// Do not reuse this handler behind any route that is not Admin-only.
/// </remarks>
public record GetOrderForStaffQuery(Guid OrderId) : IRequest<OrderDetailResponse>;

public class GetOrderForStaffQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrderForStaffQuery, OrderDetailResponse>
{
    private readonly IOrderRepository _orders = orders;

    public async Task<OrderDetailResponse> Handle(GetOrderForStaffQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        return OrderMapping.ToDetail(order);
    }
}
