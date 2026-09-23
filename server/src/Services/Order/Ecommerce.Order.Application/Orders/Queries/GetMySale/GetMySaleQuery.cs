using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMySale;

/// <summary>One of the caller's sales: their own lines of one order (specs/034).</summary>
public record GetMySaleQuery(Guid OrderId) : IRequest<SaleDetailResponse>;

public class GetMySaleQueryHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IRequestHandler<GetMySaleQuery, SaleDetailResponse>
{
    private readonly IOrderRepository _orders = orders;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<SaleDetailResponse> Handle(GetMySaleQuery request, CancellationToken cancellationToken)
    {
        var sellerId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        // One exception for four situations: no such order, nothing of theirs on it, failed, still
        // settling. A 403, or a second sentence, for any of them would confirm that the id is real and
        // that somebody else sold something on it (specs/027, research D5).
        return await _orders.GetSaleAsync(request.OrderId, sellerId, cancellationToken)
            ?? throw new NotFoundException(Sales.NotFound);
    }
}
