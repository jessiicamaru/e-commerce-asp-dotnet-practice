using Ecommerce.Order.Domain.Enums;
using Ecommerce.Order.Infrastructure.Persistence;
using Ecommerce.Shared.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecommerce.Order.Tests;

/// <summary>
/// Checkout's metrics are read from committed rows (specs/148), never counted in a handler: a consume the transient
/// retry (specs/145) rolls back and runs again must not count twice, and a rolled-back order must not count at all.
/// </summary>
[Collection(nameof(OrderTestCollection))]
public class OrderMetricsTests(OrderTestFixture fixture)
{
    private readonly OrderTestFixture _fixture = fixture;

    private async Task<double> PaidAsync()
    {
        await using var scope = _fixture.NewScope();
        var readings = await OrderMetrics.OrdersByStatusAsync(scope.ServiceProvider.GetRequiredService<OrderDbContext>(), default);
        return readings.Where(r => r.Labels.Any(l => l.Key == "status" && (string?)l.Value == "Paid")).Sum(r => r.Value);
    }

    private static Domain.Entities.Order Paid(DateTime createdAt, DateTime paidAt)
    {
        var id = Guid.CreateVersion7();
        return new Domain.Entities.Order
        {
            Id = id, UserId = Guid.CreateVersion7(), TotalAmount = 10m, Status = OrderStatus.Paid, Currency = "VND",
            CreatedAt = createdAt, UpdatedAt = paidAt, PaidAt = paidAt,
        };
    }

    [Fact]
    public async Task A_committed_order_is_counted_and_a_rolled_back_one_is_not()
    {
        var before = await PaidAsync();

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            // A transaction opened by hand runs inside the execution strategy, as production's does (CLAUDE.md).
            await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                context.Orders.Add(Paid(DateTime.UtcNow, DateTime.UtcNow));
                await context.SaveChangesAsync();
                // The consume's transaction lost its race and is rolled back, to be retried (specs/145).
                await transaction.RollbackAsync();
            });
        }

        Assert.Equal(before, await PaidAsync());

        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            context.Orders.Add(Paid(DateTime.UtcNow, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        Assert.Equal(before + 1, await PaidAsync());
    }

    [Fact]
    public async Task Settle_time_is_the_percentiles_of_the_last_minute_s_payments()
    {
        // A minute nobody else's orders fall in: the far future.
        var now = new DateTime(2099, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var scope = _fixture.NewScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            context.Orders.AddRange(
                Paid(now.AddSeconds(-11), now.AddSeconds(-10)),        // 1 s
                Paid(now.AddSeconds(-22), now.AddSeconds(-20)),        // 2 s
                Paid(now.AddSeconds(-40), now.AddSeconds(-30)),        // 10 s
                Paid(now.AddSeconds(-200), now.AddSeconds(-90)));      // paid outside the window
            await context.SaveChangesAsync();
        }

        await using var read = _fixture.NewScope();
        var readings = await OrderMetrics.SettlePercentilesAsync(read.ServiceProvider.GetRequiredService<OrderDbContext>(), now, default);

        var byQuantile = readings.ToDictionary(r => (string)r.Labels.Single(l => l.Key == "quantile").Value!, r => r.Value);
        Assert.Equal(2, byQuantile["0.5"], precision: 3);
        Assert.Equal(10, byQuantile["0.95"], precision: 3);
        Assert.Equal(10, byQuantile["0.99"], precision: 3);
    }

    [Fact]
    public async Task No_payments_in_the_window_report_no_settle_time_rather_than_zero()
    {
        await using var scope = _fixture.NewScope();
        var readings = await OrderMetrics.SettlePercentilesAsync(
            scope.ServiceProvider.GetRequiredService<OrderDbContext>(), new DateTime(2199, 1, 1, 0, 0, 0, DateTimeKind.Utc), default);

        Assert.Empty(readings);
    }

    [Fact]
    public async Task A_sample_that_fails_keeps_the_gauge_s_previous_readings()
    {
        var fail = false;
        var gauge = new SampledGauge("test.gauge", "{thing}", "a test", (_, _) =>
            fail ? throw new InvalidOperationException("the database is away") : Task.FromResult<IReadOnlyList<GaugeReading>>([new GaugeReading(7)]));
        var services = new ServiceCollection().BuildServiceProvider();
        var sampler = new GaugeSampler([gauge], services.GetRequiredService<IServiceScopeFactory>(), NullLogger<GaugeSampler>.Instance);

        await sampler.SampleAllAsync(default);
        fail = true;
        await sampler.SampleAllAsync(default);

        Assert.Equal(7, Assert.Single(gauge.Latest).Value);
    }

    [Fact]
    public void Without_a_metrics_endpoint_nothing_is_registered_and_nothing_is_queried()
    {
        Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("METRICS_ENDPOINT")), "this test needs METRICS_ENDPOINT unset");

        var services = new ServiceCollection().AddOrderMetrics();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(SampledGauge));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(GaugeSampler));
    }
}
