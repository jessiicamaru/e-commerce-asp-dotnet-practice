using System.Text.RegularExpressions;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetOrdersForStaff;

/// <summary>
/// Staff: find any order - by the start of its id, by its customer, in any status - newest first (#194, specs/096).
/// </summary>
/// <remarks>
/// ⚠️ Owner-unscoped, like <c>GetOrderForStaffQuery</c> (specs/038): the Admin role on the route is the whole permission,
/// so this handler must never sit behind any other route. Search by email is the storefront's to compose: it asks
/// Identity who has the address and passes the id here - Order knows user ids, never emails.
/// </remarks>
public record GetOrdersForStaffQuery(
    string? Status = null,
    string? Search = null,
    Guid? CustomerId = null,
    int Page = 1,
    int PageSize = 12) : IRequest<PagedResponse<StaffOrderSummaryResponse>>;

/// <summary>One order in the staff search: the customer's list row, plus whose it is.</summary>
public record StaffOrderSummaryResponse(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    string Status,
    string? FailureReason,
    int ItemCount,
    DateTime CreatedAt,
    string Currency,
    int ShipmentCount,
    int ShipmentsShipped);

public partial class GetOrdersForStaffQueryValidator : AbstractValidator<GetOrdersForStaffQuery>
{
    /// <summary>What staff look for. Pending and StockReserved are never reached; Completed reads as Paid.</summary>
    public static readonly string[] Allowed = ["Submitted", "Paid", "Preparing", "Shipped", "Failed", "Cancelled"];

    public GetOrdersForStaffQueryValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is null || Allowed.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: " + string.Join(", ", Allowed) + ".");
        // Four characters at least: two would match a sixteenth of the table, which is not a search.
        RuleFor(x => x.Search)
            .Must(s => s is null || IdPrefix().IsMatch(s.Trim()))
            .WithMessage("Search is the start of an order id: 4 to 36 hexadecimal characters or hyphens.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }

    [GeneratedRegex("^[0-9a-fA-F-]{4,36}$")]
    private static partial Regex IdPrefix();
}

public class GetOrdersForStaffQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrdersForStaffQuery, PagedResponse<StaffOrderSummaryResponse>>
{
    private readonly IOrderRepository _orders = orders;

    public async Task<PagedResponse<StaffOrderSummaryResponse>> Handle(GetOrdersForStaffQuery request, CancellationToken cancellationToken)
    {
        var status = request.Status is null ? (OrderStatus?)null : Enum.Parse<OrderStatus>(request.Status, ignoreCase: true);
        var prefix = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim().ToLowerInvariant();
        var (rows, total) = await _orders.SearchForStaffAsync(status, prefix, request.CustomerId, request.Page, request.PageSize, cancellationToken);
        return new PagedResponse<StaffOrderSummaryResponse>(rows, request.Page, request.PageSize, total);
    }
}
