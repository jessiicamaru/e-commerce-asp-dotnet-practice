using System.Reflection;
using Ecommerce.Shared.Messaging;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Exceptions;

namespace Ecommerce.Inventory.Tests;

/// <summary>
/// Every service reconnects to RabbitMQ within seconds of its return (specs/154, #304). MassTransit 8.3's RabbitMQ host
/// retries a lost connection up to 30 s apart, and the bus outbox delivers nothing until every endpoint is back: after a
/// minute's outage the backlog took 51-99 s to clear. <see cref="BrokerReconnect"/> puts the same filters on a 1-5 s
/// schedule by replacing a policy that has no setter, so these tests read it back from a real RabbitMQ bus
/// configuration - a MassTransit release that moves the field fails here, not silently in production.
/// </summary>
public class BrokerReconnectTests
{
    /// <summary>Builds the bus's configuration as a service does - no broker is contacted - and hands back its configurator.</summary>
    private static async Task<(IRabbitMqBusFactoryConfigurator Configurator, bool Applied)> ConfigureAsync(
        bool reconnectQuickly, string? setting = null)
    {
        IRabbitMqBusFactoryConfigurator? configurator = null;
        var applied = false;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(setting is null ? [] : [new KeyValuePair<string, string?>(BrokerReconnect.Setting, setting)])
            .Build();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddMassTransit(x => x.UsingRabbitMq((context, cfg) =>
            {
                if (reconnectQuickly)
                {
                    applied = cfg.ReconnectQuickly(context);
                }

                configurator = cfg;
            }))
            .BuildServiceProvider();
        _ = provider.GetRequiredService<IBusControl>();
        return (configurator!, applied);
    }

    private static int MaxIntervalMilliseconds(IRetryPolicy? policy) =>
        (int)policy!.GetType().GetField("_maxInterval", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy)!;

    [Fact]
    public async Task A_service_reconnects_at_most_five_seconds_apart()
    {
        var (configurator, applied) = await ConfigureAsync(reconnectQuickly: true);

        Assert.True(applied);
        Assert.Same(BrokerReconnect.Policy, BrokerReconnect.CurrentPolicy(configurator));
        Assert.Equal(5_000, MaxIntervalMilliseconds(BrokerReconnect.CurrentPolicy(configurator)));
    }

    [Fact]
    public async Task Without_it_MassTransit_waits_up_to_thirty_seconds()
    {
        // The test above reads the real thing: untouched, the host's policy is MassTransit's own, capped at 30 s.
        var (configurator, _) = await ConfigureAsync(reconnectQuickly: false);

        Assert.NotSame(BrokerReconnect.Policy, BrokerReconnect.CurrentPolicy(configurator));
        Assert.Equal(30_000, MaxIntervalMilliseconds(BrokerReconnect.CurrentPolicy(configurator)));
    }

    [Fact]
    public async Task Switched_off_by_configuration_MassTransits_schedule_stays()
    {
        // Messaging:ReconnectQuickly=false - the A/B baseline of specs/154, and the way back without a release.
        var (configurator, applied) = await ConfigureAsync(reconnectQuickly: true, setting: "false");

        Assert.False(applied);
        Assert.Equal(30_000, MaxIntervalMilliseconds(BrokerReconnect.CurrentPolicy(configurator)));
    }

    [Fact]
    public async Task Only_the_schedule_changes_never_what_is_retried()
    {
        var (configurator, _) = await ConfigureAsync(reconnectQuickly: false);
        var masstransits = BrokerReconnect.CurrentPolicy(configurator)!;

        Exception[] failures =
        [
            new RabbitMqConnectionException("Broker unreachable: guest@rabbitmq:5672/"),
            new EndOfStreamException(),
            new NotSupportedException("Pipelining of requests forbidden"),
            new NotSupportedException("something else"),
            new AuthenticationFailureException("ACCESS_REFUSED"),
            new InvalidOperationException("a bug"),
        ];

        foreach (var failure in failures)
        {
            Assert.Equal(masstransits.IsHandled(failure), BrokerReconnect.Policy.IsHandled(failure));
        }

        Assert.True(BrokerReconnect.Policy.IsHandled(failures[0]));
        Assert.False(BrokerReconnect.Policy.IsHandled(failures[4]));   // wrong credentials are never retried quickly
    }
}
