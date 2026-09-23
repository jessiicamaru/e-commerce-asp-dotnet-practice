using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetOrdersForFulfilment;

/// <summary>Staff: every customer's orders in one fulfilment status.</summary>
public record GetOrdersForFulfilmentQuery(string Status = "Paid", int Page = 1, int PageSize = 12)
    : IRequest<PagedResponse<OrderSummaryResponse>>;

public class GetOrdersForFulfilmentQueryValidator : AbstractValidator<GetOrdersForFulfilmentQuery>
{
    public static readonly string[] Allowed = ["Paid", "Preparing", "Shipped"];

    public GetOrdersForFulfilmentQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => Allowed.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: " + string.Join(", ", Allowed) + ".");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetOrdersForFulfilmentQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrdersForFulfilmentQuery, PagedResponse<OrderSummaryResponse>>
{
    private readonly IOrderRepository _orders = orders;

    public async Task<PagedResponse<OrderSummaryResponse>> Handle(
        GetOrdersForFulfilmentQuery request, CancellationToken cancellationToken)
    {
        var status = Enum.Parse<OrderStatus>(request.Status, ignoreCase: true);
        var (rows, total) = await _orders.GetPageByStatusAsync(status, request.Page, request.PageSize, cancellationToken);
        return new PagedResponse<OrderSummaryResponse>(rows, request.Page, request.PageSize, total);
    }
}
