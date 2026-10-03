using Ecommerce.Shared.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// A consumer whose transaction PostgreSQL aborts with a serialization failure is tried again, and succeeds (specs/145,
/// #299). Without it, the load test of #290 left a paid order's stock confirmation in Inventory's error queue and its
/// units held for the expiry sweeper to resell. A failure that is not transient still faults at once.
/// </summary>
public class TransientRetryTests
{
    public record Confirm(Guid Id);

    /// <summary>Shaped like Npgsql's PostgresException: what the policy reads is the SqlState, by name.</summary>
    public class FakePostgresException(string sqlState) : Exception($"{sqlState}: could not serialize access due to concurrent update")
    {
        public string SqlState { get; } = sqlState;
    }

    public class Flaky : IConsumer<Confirm>
    {
        public static int FailuresLeft;
        public static int Attempts;
        public static Func<Exception> Failure = () => new FakePostgresException("40001");

        public Task Consume(ConsumeContext<Confirm> context)
        {
            Interlocked.Increment(ref Attempts);
            if (Interlocked.Decrement(ref FailuresLeft) >= 0)
            {
                // As EF wraps it: the database's error inside the save's own exception.
                throw new InvalidOperationException("An exception occurred while saving.", Flaky.Failure());
            }

            return Task.CompletedTask;
        }
    }

    private static async Task<ITestHarness> HarnessAsync(bool withRetry)
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<Flaky>();
                if (withRetry)
                {
                    x.AddConfigureEndpointsCallback((_, _, cfg) => cfg.UseTransientRetry());
                }
            })
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return harness;
    }

    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    public async Task A_serialization_failure_or_a_deadlock_is_tried_again_and_succeeds(string sqlState)
    {
        Flaky.FailuresLeft = 3;
        Flaky.Attempts = 0;
        Flaky.Failure = () => new FakePostgresException(sqlState);
        var harness = await HarnessAsync(withRetry: true);

        await harness.Bus.Publish(new Confirm(Guid.CreateVersion7()));

        var consumer = harness.GetConsumerHarness<Flaky>();
        Assert.True(await consumer.Consumed.Any<Confirm>());
        await harness.InactivityTask;
        Assert.Equal(4, Flaky.Attempts);
        Assert.False(await harness.Published.Any<Fault<Confirm>>());
    }

    [Fact]
    public async Task Without_the_policy_the_first_serialization_failure_faults_the_message()
    {
        Flaky.FailuresLeft = 1;
        Flaky.Attempts = 0;
        Flaky.Failure = () => new FakePostgresException("40001");
        var harness = await HarnessAsync(withRetry: false);

        await harness.Bus.Publish(new Confirm(Guid.CreateVersion7()));

        Assert.True(await harness.Published.Any<Fault<Confirm>>());
        Assert.Equal(1, Flaky.Attempts);
    }

    [Fact]
    public async Task A_failure_that_is_not_transient_faults_at_once()
    {
        Flaky.FailuresLeft = 1;
        Flaky.Attempts = 0;
        Flaky.Failure = () => new ArgumentException("a bug, not a collision");
        var harness = await HarnessAsync(withRetry: true);

        await harness.Bus.Publish(new Confirm(Guid.CreateVersion7()));

        Assert.True(await harness.Published.Any<Fault<Confirm>>());
        Assert.Equal(1, Flaky.Attempts);
    }

    [Fact]
    public void Only_serialization_failures_deadlocks_and_transient_connections_count()
    {
        Assert.True(TransientRetry.IsTransient(new FakePostgresException("40001")));
        Assert.True(TransientRetry.IsTransient(new Exception("outer", new FakePostgresException("40P01"))));
        Assert.False(TransientRetry.IsTransient(new FakePostgresException("23505")));   // a unique violation is a decision
        Assert.False(TransientRetry.IsTransient(new TimeoutException()));
        Assert.False(TransientRetry.IsTransient(new InvalidOperationException("outer", new ArgumentException())));
    }
}
