using Ecommerce.Order.Application.Orders.Common;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMyOrders;

/// <summary>
/// The caller's own orders, newest first.
/// </summary>
/// <remarks>
/// There is deliberately <b>no user id here</b>, exactly as on <c>SubmitOrderCommand</c>. The
/// shopper whose orders come back is read from the validated token through <c>ICurrentUser</c>. A
/// user id on this record would be a field a caller could set, which is how this project once let
/// anyone place an order on anyone's behalf.
/// </remarks>
public record GetMyOrdersQuery(
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResponse<OrderSummaryResponse>>;
