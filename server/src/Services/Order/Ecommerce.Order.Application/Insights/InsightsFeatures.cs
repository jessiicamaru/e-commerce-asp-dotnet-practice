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

public record RevenueResponse(DateTime From, DateTime To, List<RevenueTotal> Totals, List<RevenueDay> Days);

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

/// <summary>The rows the insights are built from; the repository does the grouping in SQL.</summary>
public interface IOrderInsights
{
    Task<List<RevenueRow>> RevenueByDayAsync(DateTime from, DateTime to, CancellationToken cancellationToken);

    Task<List<ProductSalesRow>> ProductSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken);

    Task<List<BuyerRow>> BuyersAsync(DateTime from, DateTime to, CancellationToken cancellationToken);
}

/// <summary>One currency's paid orders on one day. Currency null: an order from before specs/022.</summary>
public record RevenueRow(DateTime Day, string? Currency, decimal Revenue, int Orders);

public record ProductSalesRow(Guid ProductId, string ProductName, string? Currency, int Units, decimal Revenue);

public record BuyerRow(Guid CustomerId, string? Currency, int Orders, decimal Spent);

public static class InsightsPeriod
{
    public const int MaxDays = 366;

    /// <summary>The last 30 days unless told otherwise; <c>to</c> is exclusive.</summary>
    public static (DateTime From, DateTime To) Resolve(DateTime? from, DateTime? to)
    {
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        return (start, end);
    }

    public static IRuleBuilderOptions<T, DateTime?> NotAfter<T>(this IRuleBuilder<T, DateTime?> rule, Func<T, DateTime?> to) =>
        rule.Must((q, from) => from is null || to(q) is null || from < to(q)).WithMessage("The period must start before it ends.");
}

public class GetRevenueQueryValidator : AbstractValidator<GetRevenueQuery>
{
    public GetRevenueQueryValidator()
    {
        RuleFor(x => x.From).NotAfter(x => x.To);
        RuleFor(x => x).Must(x => Span(x.From, x.To) <= InsightsPeriod.MaxDays)
            .WithMessage($"The period can be at most {InsightsPeriod.MaxDays} days.");
    }

    internal static double Span(DateTime? from, DateTime? to)
    {
        var (start, end) = InsightsPeriod.Resolve(from, to);
        return (end - start).TotalDays;
    }
}

public class GetTopProductsQueryValidator : AbstractValidator<GetTopProductsQuery>
{
    public GetTopProductsQueryValidator()
    {
        RuleFor(x => x.From).NotAfter(x => x.To);
        RuleFor(x => x.By).Must(b => b is "units" or "revenue").WithMessage("By must be units or revenue.");
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class GetTopBuyersQueryValidator : AbstractValidator<GetTopBuyersQuery>
{
    public GetTopBuyersQueryValidator()
    {
        RuleFor(x => x.From).NotAfter(x => x.To);
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}

public class InsightsHandlers(IOrderInsights insights, IOptions<CurrencyOptions> money) :
    IRequestHandler<GetRevenueQuery, RevenueResponse>,
    IRequestHandler<GetTopProductsQuery, List<TopProduct>>,
    IRequestHandler<GetTopBuyersQuery, List<TopBuyer>>
{
    private readonly IOrderInsights _insights = insights;
    private readonly string _default = money.Value.DefaultCurrency;

    public async Task<RevenueResponse> Handle(GetRevenueQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = InsightsPeriod.Resolve(request.From, request.To);
        var rows = (await _insights.RevenueByDayAsync(from, to, cancellationToken))
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

        return new RevenueResponse(from, to, totals, days);
    }

    public async Task<List<TopProduct>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = InsightsPeriod.Resolve(request.From, request.To);
        var currency = request.Currency ?? _default;
        var rows = await _insights.ProductSalesAsync(from, to, cancellationToken);

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

        var ordered = request.By == "revenue"
            ? products.OrderByDescending(p => p.Revenue.FirstOrDefault(r => r.Currency == currency)?.Amount ?? 0).ThenByDescending(p => p.Units)
            : products.OrderByDescending(p => p.Units).ThenBy(p => p.ProductName);

        return ordered.Take(request.Limit).ToList();
    }

    public async Task<List<TopBuyer>> Handle(GetTopBuyersQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = InsightsPeriod.Resolve(request.From, request.To);
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
