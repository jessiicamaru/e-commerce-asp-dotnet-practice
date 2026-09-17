using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyOrderById;

public class GetMyOrderByIdQueryHandler(
    IOrderRepository orderRepository,
    ICurrentUser currentUser
) : IRequestHandler<GetMyOrderByIdQuery, OrderDetailResponse>
{
    private readonly IOrderRepository _orderRepository = orderRepository;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<OrderDetailResponse> Handle(
        GetMyOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var order = await _orderRepository.GetByIdForUserAsync(request.OrderId, userId, cancellationToken);

        // One exception for two situations, on purpose. An order that does not exist and an order
        // belonging to somebody else must be answered identically, or the response itself confirms
        // which ids are real. A ConflictException or a 403 here would be the disclosure.
        if (order is null)
        {
            throw new NotFoundException("Order not found.");
        }

        return new OrderDetailResponse(
            order.Id,
            order.UserId,
            order.TotalAmount,
            order.Status.ToString(),
            order.FailureReason,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items
                .Select(x => new OrderItemDetailResponse(
                    x.ProductId,
                    x.ProductName,
                    x.Quantity,
                    x.UnitPrice,
                    x.TotalPrice))
                .ToList());
    }
}
