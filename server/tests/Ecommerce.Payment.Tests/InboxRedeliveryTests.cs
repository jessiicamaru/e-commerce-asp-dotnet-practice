using Ecommerce.Payment.Infrastructure.Persistence;
using Ecommerce.Shared.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// One message delivered twice at once is consumed once and faults neither time (specs/149, #306).
/// <para>
/// When RabbitMQ comes back after an outage, a delivery that was never acknowledged is delivered again while its first
/// consume may still be running. Both consumes insert the same inbox row; the second waits for the first to commit and
/// then fails with a unique violation on <c>AK_InboxState_MessageId_ConsumerId</c>. The broker-fault run of #292 left
/// a <c>ProcessPaymentCommand</c> in <c>ProcessPayment_error</c> exactly that way, for an order its twin had paid.
/// Tried again, the second consume finds the inbox row consumed and drops the duplicate - which is the inbox's job.
/// </para>
/// <para>
/// Against the real EF outbox and inbox on PostgreSQL, because the guarantee is the inbox's unique key and the
/// isolation the outbox consumes in: a fake would only restate the policy.
/// </para>
/// </summary>
[Collection(nameof(PaymentTestCollection))]
public class InboxRedeliveryTests(PaymentTestFixture fixture)
{
    public record Charge(Guid OrderId);

    public class SlowCharge : IConsumer<Charge>
    {
        public static int Runs;

        public async Task Consume(ConsumeContext<Charge> context)
        {
            Interlocked.Increment(ref Runs);
            // Long enough for the duplicate to reach the inbox while this transaction still holds its row.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }

    /// <summary>
    /// What became of each delivery. The harness keeps one record per message id, so a duplicate would hide behind its
    /// twin; a receive observer is called for every delivery.
    /// </summary>
    public class Deliveries : IReceiveObserver
    {
        public readonly List<Exception> Faults = [];

        public Task PreReceive(ReceiveContext context) => Task.CompletedTask;
        public Task PostReceive(ReceiveContext context) => Task.CompletedTask;
        public Task PostConsume<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType) where T : class => Task.CompletedTask;
        public Task ConsumeFault<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType, Exception exception) where T : class => Fault(exception);

        // The inbox runs before the consumer, so its failure is the receive's - this is what reaches an _error queue.
        public Task ReceiveFault(ReceiveContext context, Exception exception) => Fault(exception);

        private Task Fault(Exception exception)
        {
            lock (Faults)
            {
                Faults.Add(exception);
            }

            return Task.CompletedTask;
        }
    }

    private async Task<(ServiceProvider Provider, ITestHarness Harness)> HarnessAsync(bool withRetry)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<PaymentDbContext>(options => options.UseNpgsql(fixture.ConnectionString))
            .AddSingleton<Deliveries>()
            .AddReceiveObserver(sp => sp.GetRequiredService<Deliveries>())
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<SlowCharge>();
                x.AddEntityFrameworkOutbox<PaymentDbContext>(o => o.UsePostgres());
                // The order every service configures: the retry wraps the outbox, so each attempt is a new transaction.
                x.AddConfigureEndpointsCallback((context, _, cfg) =>
                {
                    // Two at once, as two channels of a reconnected broker deliver them.
                    cfg.ConcurrentMessageLimit = 2;
                    if (withRetry)
                    {
                        cfg.UseTransientRetry();
                    }

                    cfg.UseEntityFrameworkOutbox<PaymentDbContext>(context);
                });
            })
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (provider, harness);
    }

    private static async Task<Deliveries> DeliverTwiceAtOnceAsync(ServiceProvider provider, ITestHarness harness)
    {
        var messageId = NewId.NextGuid();
        var charge = new Charge(Guid.CreateVersion7());
        await Task.WhenAll(
            harness.Bus.Publish(charge, c => c.MessageId = messageId),
            harness.Bus.Publish(charge, c => c.MessageId = messageId));
        await harness.InactivityTask;
        return provider.GetRequiredService<Deliveries>();
    }

    [Fact]
    public async Task A_message_delivered_twice_at_once_is_consumed_once_and_never_faults()
    {
        SlowCharge.Runs = 0;
        var (provider, harness) = await HarnessAsync(withRetry: true);
        await using var _ = provider;

        var deliveries = await DeliverTwiceAtOnceAsync(provider, harness);

        Assert.Equal(1, SlowCharge.Runs);
        Assert.Empty(deliveries.Faults);
    }

    [Fact]
    public async Task Without_the_policy_the_duplicate_faults_on_the_inbox_key()
    {
        SlowCharge.Runs = 0;
        var (provider, harness) = await HarnessAsync(withRetry: false);
        await using var _ = provider;

        var deliveries = await DeliverTwiceAtOnceAsync(provider, harness);

        var fault = Assert.Single(deliveries.Faults);
        Assert.Contains("AK_InboxState_MessageId_ConsumerId", (fault.InnerException ?? fault).Message);
        Assert.Equal(1, SlowCharge.Runs);
    }
}
