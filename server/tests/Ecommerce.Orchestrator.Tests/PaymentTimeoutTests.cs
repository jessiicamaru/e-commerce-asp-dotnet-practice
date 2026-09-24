using Ecommerce.Orchestrator.WebApi.StateMachines;
using Ecommerce.Orchestrator.WebApi.Timeouts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Ecommerce.Orchestrator.Tests;

/// <summary>
/// Which orders the sweeper finds, against the real saga table (specs/053) - a query over state NAMES and
/// timestamps, which an in-memory repository would answer however the test assumed.
/// </summary>
public sealed class PaymentTimeoutTests : IAsyncLifetime
{
    private const string Admin = "Host=localhost;Port=5436;Database=postgres;Username=postgres;Password={0}";

    private string _database = null!;
    private string _connection = null!;

    public async Task InitializeAsync()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        _database = $"orchestrator_tests_{Guid.NewGuid():N}";
        _connection = $"Host=localhost;Port=5436;Database={_database};Username=postgres;Password={password};Maximum Pool Size=10";

        await using (var admin = new NpgsqlConnection(string.Format(Admin, password)))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        await using var admin = new NpgsqlConnection(string.Format(Admin, password));
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Only_orders_still_waiting_for_Payment_past_the_cutoff_are_due()
    {
        var now = DateTime.UtcNow;
        var stale = await SeedAsync(PaymentTimeouts.AwaitingPayment, now.AddMinutes(-11));
        await SeedAsync(PaymentTimeouts.AwaitingPayment, now.AddMinutes(-2));      // still within its time
        await SeedAsync("Submitted", now.AddMinutes(-30));                            // not reserved yet
        await SeedAsync("PaymentTimedOut", now.AddMinutes(-30));                      // already failed

        await using var context = NewContext();
        var due = await PaymentTimeouts.DueAsync(context, now.AddMinutes(-10));

        Assert.Equal([stale], due);
    }

    [Fact]
    public async Task Expiring_announces_each_due_order_to_the_saga()
    {
        var now = DateTime.UtcNow;
        var a = await SeedAsync(PaymentTimeouts.AwaitingPayment, now.AddMinutes(-15));
        var b = await SeedAsync(PaymentTimeouts.AwaitingPayment, now.AddMinutes(-12));

        await using var provider = new ServiceCollection().AddMassTransitTestHarness().BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await using var context = NewContext();
        var count = await PaymentTimeouts.ExpireAsync(context, harness.Bus, now.AddMinutes(-10));

        Assert.Equal(2, count);
        var announced = harness.Published.Select<PaymentTimeoutExpired>().Select(m => m.Context.Message.OrderId).ToHashSet();
        Assert.Equal(new HashSet<Guid> { a, b }, announced);
    }

    // ------------------------------------------------------------------ options

    [Fact]
    public void The_defaults_wait_ten_minutes_and_look_every_thirty_seconds()
    {
        var options = PaymentTimeoutOptions.From(_ => null);

        Assert.Equal((TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(30)), (options.Timeout, options.SweepInterval));
    }

    /// <summary>The whole point is to fail the order while its stock is still held.</summary>
    [Fact]
    public void A_timeout_that_is_not_shorter_than_the_inventory_hold_refuses_to_start()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => PaymentTimeoutOptions.From(Settings(
            (PaymentTimeoutOptions.TimeoutVariable, "900"), (PaymentTimeoutOptions.InventoryHoldVariable, "15"))));

        Assert.Contains(PaymentTimeoutOptions.InventoryHoldVariable, refused.Message);
        Assert.Contains(PaymentTimeoutOptions.TimeoutVariable, refused.Message);

        // Shorter by more than one sweep is fine.
        PaymentTimeoutOptions.From(Settings((PaymentTimeoutOptions.TimeoutVariable, "600"), (PaymentTimeoutOptions.InventoryHoldVariable, "15")));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("ten minutes")]
    public void A_timeout_that_is_not_a_positive_number_of_seconds_refuses_to_start(string value)
    {
        var refused = Assert.Throws<InvalidOperationException>(() =>
            PaymentTimeoutOptions.From(Settings((PaymentTimeoutOptions.TimeoutVariable, value))));

        Assert.Contains(value, refused.Message);
    }

    // ------------------------------------------------------------------ helpers

    private static Func<string, string?> Settings(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;

    private OrchestratorDbContext NewContext() =>
        new(new DbContextOptionsBuilder<OrchestratorDbContext>().UseNpgsql(_connection).Options);

    private async Task<Guid> SeedAsync(string state, DateTime updatedAt)
    {
        var id = Guid.NewGuid();
        await using var context = NewContext();
        context.Set<OrderStateData>().Add(new OrderStateData
        {
            CorrelationId = id,
            CurrentState = state,
            UserId = Guid.NewGuid(),
            TotalAmount = 125_000m,
            Currency = "VND",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
        await context.SaveChangesAsync();
        return id;
    }
}
