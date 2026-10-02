using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetMySales;

/// <summary>
/// The caller's sales (specs/034): the paid orders holding at least one of their lines.
/// </summary>
/// <remarks>
/// There is no seller id here and there must never be one. The seller is the token's subject, for the
/// same reason <c>SubmitOrderCommand</c> has no user id (Constitution IV).
/// </remarks>
/// <param name="Status">
/// Optionally the state of the caller's own part (specs/131): <c>Paid</c> - waiting to be prepared, neither it nor the
/// order cancelled - <c>Preparing</c>, <c>Shipped</c>, or <c>Cancelled</c> (the part or the whole order).
/// </param>
public record GetMySalesQuery(int Page = 1, int PageSize = 12, string? Status = null) : IRequest<PagedResponse<SaleSummaryResponse>>;

public class GetMySalesQueryValidator : AbstractValidator<GetMySalesQuery>
{
    public GetMySalesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status)
            .Must(status => status is null || SalePartFilter.Names.Contains(status))
            .WithMessage($"Status must be one of: {string.Join(", ", SalePartFilter.Names)}.");
    }
}

public class GetMySalesQueryHandler(IOrderRepository orders, ICurrentUser currentUser)
    : IRequestHandler<GetMySalesQuery, PagedResponse<SaleSummaryResponse>>
{
    private readonly IOrderRepository _orders = orders;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PagedResponse<SaleSummaryResponse>> Handle(
        GetMySalesQuery request, CancellationToken cancellationToken)
    {
        var sellerId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var (sales, total) = await _orders.GetSalesPageAsync(
            sellerId, request.Page, request.PageSize, request.Status, cancellationToken);

        return new PagedResponse<SaleSummaryResponse>(sales, request.Page, request.PageSize, total);
    }
}

/// <summary>The states a seller's sales can be filtered by (specs/131) - of their own part, as the sale page shows it.</summary>
public static class SalePartFilter
{
    public const string Paid = "Paid";
    public const string Preparing = "Preparing";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> Names = [Paid, Preparing, Shipped, Cancelled];
}
