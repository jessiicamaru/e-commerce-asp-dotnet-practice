using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Insights;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Application.Insights;

/// <summary>
/// How the shop is doing, for administrators (specs/047). Every amount is PER CURRENCY: dong and dollars
/// are never added together, because nothing here converts one into the other (specs/022).
/// </summary>
public record RevenueTotal(string Currency, decimal Revenue, int Orders, decimal AverageOrderValue);

public record RevenueDay(DateOnly Day, string Currency, decimal Revenue, int Orders);

/// <param name="FirstDay">The first of the shop's days the period covers (specs/082) - the chart draws from it.</param>
/// <param name="LastDay">The last of them, included.</param>
public record RevenueResponse(DateTime From, DateTime To, List<RevenueTotal> Totals, List<RevenueDay> Days, DateOnly FirstDay, DateOnly LastDay);

public record CurrencyAmount(string Currency, decimal Amount);

public record TopProduct(Guid ProductId, string ProductName, int Units, List<CurrencyAmount> Revenue);

public record TopBuyer(Guid CustomerId, int Orders, List<CurrencyAmount> Spent);

/// <summary>Revenue over a period: paid orders, never failed, never cancelled, never still settling.</summary>
public record GetRevenueQuery(DateTime? From = null, DateTime? To = null) : IRequest<RevenueResponse>;

/// <summary>What sold most, by units or by revenue in one currency.</summary>
public record GetTopProductsQuery(DateTime? From = null, DateTime? To = null, string By = "units", string? Currency = null, int Limit = 10)
    : IRequest<List<TopProduct>>;

/// <summary>Who spent most, in one currency. Ids only: Order does not know emails - Identity does.</summary>
public record GetTopBuyersQuery(DateTime? From = null, DateTime? To = null, string? Currency = null, int Limit = 10)
    : IRequest<List<TopBuyer>>;

/// <summary>
/// The signed-in seller's revenue (specs/068, #111): the sum of THEIR lines - unit price × quantity, before tax -
/// on sold orders, per currency. Never the order's total, which holds other sellers' goods, delivery and tax
/// (specs/034), and never a part that came back and was refunded (specs/066). The seller is the token's.
/// </summary>
public record GetSellerRevenueQuery(DateTime? From = null, DateTime? To = null) : IRequest<RevenueResponse>;

/// <summary>The signed-in seller's best-selling products, by units - the same rows as their revenue.</summary>
public record GetSellerTopProductsQuery(DateTime? From = null, DateTime? To = null, int Limit = 10) : IRequest<List<TopProduct>>;

/// <summary>The rows the insights are built from; the repository does the grouping in SQL.</summary>
public interface IOrderInsights
{
    /// <summary>Sold orders per shop day (in <paramref name="timeZone"/>, an IANA id - specs/082) and currency.</summary>
    Task<List<RevenueRow>> RevenueByDayAsync(DateTime from, DateTime to, string timeZone, CancellationToken cancellationToken);

    Task<List<ProductSalesRow>> ProductSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken);

    Task<List<BuyerRow>> BuyersAsync(DateTime from, DateTime to, CancellationToken cancellationToken);

    /// <summary>One seller's own lines per day and currency: revenue over the lines, orders counted once each.</summary>
    Task<List<RevenueRow>> SellerRevenueByDayAsync(Guid sellerId, DateTime from, DateTime to, string timeZone, CancellationToken cancellationToken);

    /// <summary>One seller's own lines per product and currency.</summary>
    Task<List<ProductSalesRow>> SellerProductSalesAsync(Guid sellerId, DateTime from, DateTime to, string timeZone, CancellationToken cancellationToken);
}

/// <summary>One currency's paid orders on one day. Currency null: an order from before specs/022.</summary>
public record RevenueRow(DateTime Day, string? Currency, decimal Revenue, int Orders);

public record ProductSalesRow(Guid ProductId, string ProductName, string? Currency, int Units, decimal Revenue);

public record BuyerRow(Guid CustomerId, string? Currency, int Orders, decimal Spent);

public class GetRevenueQueryValidator : AbstractValidator<GetRevenueQuery>
{
    // One period rule for every insight, Catalog's included (specs/055, #125).
    public GetRevenueQueryValidator(InsightsCalendar calendar) => this.ValidPeriod(x => x.From, x => x.To, calendar);
}

public class GetTopProductsQueryValidator : AbstractValidator<GetTopProductsQuery>
{
    public GetTopProductsQueryValidator(InsightsCalendar calendar)
    {
        this.ValidPeriod(x => x.From, x => x.To, calendar);
        RuleFor(x => x.By).Must(b => b is "units" or "revenue").WithMessage("By must be units or revenue.");
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class GetTopBuyersQueryValidator : AbstractValidator<GetTopBuyersQuery>
{
    public GetTopBuyersQueryValidator(InsightsCalendar calendar)
    {
        this.ValidPeriod(x => x.From, x => x.To, calendar);
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class GetSellerRevenueQueryValidator : AbstractValidator<GetSellerRevenueQuery>
{
    public GetSellerRevenueQueryValidator(InsightsCalendar calendar) => this.ValidPeriod(x => x.From, x => x.To, calendar);
}

public class GetSellerTopProductsQueryValidator : AbstractValidator<GetSellerTopProductsQuery>
{
    public GetSellerTopProductsQueryValidator(InsightsCalendar calendar)
    {
        this.ValidPeriod(x => x.From, x => x.To, calendar);
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class InsightsHandlers(IOrderInsights insights, IOptions<CurrencyOptions> money, ICurrentUser currentUser, InsightsCalendar calendar) :
    IRequestHandler<GetRevenueQuery, RevenueResponse>,
    IRequestHandler<GetTopProductsQuery, List<TopProduct>>,
    IRequestHandler<GetTopBuyersQuery, List<TopBuyer>>,
    IRequestHandler<GetSellerRevenueQuery, RevenueResponse>,
    IRequestHandler<GetSellerTopProductsQuery, List<TopProduct>>
{
    private readonly IOrderInsights _insights = insights;
    private readonly string _default = money.Value.DefaultCurrency;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <summary>The shop's days (specs/082): a morning in Hanoi is that day's, not the previous UTC day's.</summary>
    private readonly InsightsCalendar _calendar = calendar;

    public async Task<RevenueResponse> Handle(GetRevenueQuery request, CancellationToken cancellationToken)
    {
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, _calendar);
        return Revenue(period, await _insights.RevenueByDayAsync(period.Start, period.End, _calendar.ZoneId, cancellationToken));
    }

    // Research D2: the seller's answers have the admin's shapes, grouped by the same code - one chart draws both.

    public async Task<RevenueResponse> Handle(GetSellerRevenueQuery request, CancellationToken cancellationToken)
    {
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, _calendar);
        return Revenue(period, await _insights.SellerRevenueByDayAsync(SellerId(), period.Start, period.End, _calendar.ZoneId, cancellationToken));
    }

    public async Task<List<TopProduct>> Handle(GetSellerTopProductsQuery request, CancellationToken cancellationToken)
    {
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, _calendar);
        var rows = await _insights.SellerProductSalesAsync(SellerId(), period.Start, period.End, _calendar.ZoneId, cancellationToken);
        return TopProducts(rows, "units", _default, request.Limit);
    }

    /// <summary>The caller - never read as "the shop's own" when the token names nobody.</summary>
    private Guid SellerId() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

    private RevenueResponse Revenue(InsightsPeriod period, List<RevenueRow> found)
    {
        var (from, to) = (period.Start, period.End);
        var rows = found
            .Select(r => r with { Currency = r.Currency ?? _default })
            .ToList();

        var days = rows
            .GroupBy(r => (Day: DateOnly.FromDateTime(r.Day), r.Currency))
            .Select(g => new RevenueDay(g.Key.Day, g.Key.Currency!, g.Sum(r => r.Revenue), g.Sum(r => r.Orders)))
            .OrderBy(d => d.Day).ThenBy(d => d.Currency)
            .ToList();

        var totals = days
            .GroupBy(d => d.Currency)
            .Select(g =>
            {
                var revenue = g.Sum(d => d.Revenue);
                var orders = g.Sum(d => d.Orders);
                return new RevenueTotal(g.Key, revenue, orders, orders == 0 ? 0 : Math.Round(revenue / orders, 2, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(t => t.Revenue)
            .ToList();

        return new RevenueResponse(from, to, totals, days, period.FirstDay, period.LastDay);
    }

    public async Task<List<TopProduct>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, _calendar);
        var rows = await _insights.ProductSalesAsync(period.Start, period.End, cancellationToken);
        return TopProducts(rows, request.By, request.Currency ?? _default, request.Limit);
    }

    private List<TopProduct> TopProducts(List<ProductSalesRow> rows, string by, string currency, int limit)
    {
        var products = rows
            .GroupBy(r => r.ProductId)
            .Select(g => new TopProduct(
                g.Key,
                // The name frozen on the most recent line: a product renamed since reads as it does now.
                g.Last().ProductName,
                g.Sum(r => r.Units),
                g.GroupBy(r => r.Currency ?? _default)
                    .Select(c => new CurrencyAmount(c.Key, c.Sum(r => r.Revenue)))
                    .OrderByDescending(c => c.Amount)
                    .ToList()))
            .ToList();

        var ordered = by == "revenue"
            ? products.OrderByDescending(p => p.Revenue.FirstOrDefault(r => r.Currency == currency)?.Amount ?? 0).ThenByDescending(p => p.Units)
            : products.OrderByDescending(p => p.Units).ThenBy(p => p.ProductName);

        return ordered.Take(limit).ToList();
    }

    public async Task<List<TopBuyer>> Handle(GetTopBuyersQuery request, CancellationToken cancellationToken)
    {
        var period = InsightsPeriod.Resolve(request.From, request.To, DateTime.UtcNow, _calendar);
        var (from, to) = (period.Start, period.End);
        var currency = request.Currency ?? _default;
        var rows = await _insights.BuyersAsync(from, to, cancellationToken);

        return rows
            .GroupBy(r => r.CustomerId)
            .Select(g => new TopBuyer(
                g.Key,
                g.Sum(r => r.Orders),
                g.GroupBy(r => r.Currency ?? _default)
                    .Select(c => new CurrencyAmount(c.Key, c.Sum(r => r.Spent)))
                    .OrderByDescending(c => c.Amount)
                    .ToList()))
            .OrderByDescending(b => b.Spent.FirstOrDefault(s => s.Currency == currency)?.Amount ?? 0)
            .ThenByDescending(b => b.Orders)
            .Take(request.Limit)
            .ToList();
    }
}
