using Ecommerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Order.Infrastructure.Persistence;

/// <summary>
/// Checkout's outcomes as gauges, read from committed orders (specs/148). Counting at settlement would count a retried
/// consume twice (specs/145); the rows say what is true.
/// </summary>
public static class OrderMetrics
{
    /// <summary>Orders paid within this long before a sample give its settle-time percentiles.</summary>
    public static readonly TimeSpan SettleWindow = TimeSpan.FromSeconds(60);

    public static IServiceCollection AddOrderMetrics(this IServiceCollection services) => services
        .AddSampledGauge(
            "ecommerce.orders", "{order}", "Orders by status, as committed",
            async (sp, ct) => await OrdersByStatusAsync(sp.GetRequiredService<OrderDbContext>(), ct))
        .AddSampledGauge(
            "ecommerce.order.settle", "s", "Time from placing an order to its payment, over orders paid in the last minute",
            async (sp, ct) => await SettlePercentilesAsync(sp.GetRequiredService<OrderDbContext>(), DateTime.UtcNow, ct))
        .AddSampledGauge(
            "ecommerce.outbox.pending_messages", "{message}", "Messages written to this service's outbox, not yet delivered",
            async (sp, ct) => [new GaugeReading(await sp.GetRequiredService<OrderDbContext>()
                .Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().CountAsync(ct))]);

    public static async Task<IReadOnlyList<GaugeReading>> OrdersByStatusAsync(OrderDbContext context, CancellationToken ct)
    {
        var counts = await context.Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return counts
            .Select(c => new GaugeReading(c.Count, new KeyValuePair<string, object?>("status", c.Status.ToString())))
            .ToList();
    }

    /// <summary>p50, p95 and p99 of placed-to-paid over the window; nothing when nothing was paid in it.</summary>
    public static async Task<IReadOnlyList<GaugeReading>> SettlePercentilesAsync(OrderDbContext context, DateTime now, CancellationToken ct)
    {
        var since = now - SettleWindow;
        var times = await context.Orders.AsNoTracking()
            .Where(o => o.PaidAt != null && o.PaidAt > since)
            .Select(o => new { o.CreatedAt, PaidAt = o.PaidAt!.Value })
            .ToListAsync(ct);
        var seconds = times.Select(t => (t.PaidAt - t.CreatedAt).TotalSeconds).Order().ToList();
        if (seconds.Count == 0)
        {
            return [];
        }

        double At(double q) => seconds[Math.Min(seconds.Count - 1, (int)(q * seconds.Count))];
        return
        [
            new GaugeReading(At(0.5), new KeyValuePair<string, object?>("quantile", "0.5")),
            new GaugeReading(At(0.95), new KeyValuePair<string, object?>("quantile", "0.95")),
            new GaugeReading(At(0.99), new KeyValuePair<string, object?>("quantile", "0.99")),
        ];
    }
}
