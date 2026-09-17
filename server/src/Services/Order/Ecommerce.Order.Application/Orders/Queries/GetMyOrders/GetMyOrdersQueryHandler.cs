using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyOrders;

public class GetMyOrdersQueryHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser
) : IRequestHandler<GetMyOrdersQuery, PagedResponse<OrderSummaryResponse>>
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PagedResponse<OrderSummaryResponse>> Handle(
        GetMyOrdersQuery request,
        CancellationToken cancellationToken)
    {
        // [Authorize] already rejected anonymous callers; this guards against the endpoint being
        // wired up without it, the same way SubmitOrderCommandHandler does.
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var (orders, totalCount) = await _orderRepository.GetPageByUserAsync(
            userId, request.Page, request.PageSize, cancellationToken);

        return new PagedResponse<OrderSummaryResponse>(
            orders,
            request.Page,
            request.PageSize,
            totalCount);
    }
}
