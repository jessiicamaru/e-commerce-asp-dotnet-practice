using System.Reflection;
using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Exceptions;

namespace Ecommerce.Shared.Messaging;

/// <summary>
/// Every service reconnects to RabbitMQ within seconds of its return (specs/154, #304).
/// <para>
/// MassTransit 8.3's RabbitMQ host retries a lost connection on a policy fixed in its constructor - exponential from 3 s
/// to <b>30 s</b> - and the bus outbox delivers nothing until EVERY receive endpoint has reconnected
/// (<c>WaitForHealthStatus(Healthy)</c>). After a minute's outage each endpoint is retrying every 30 s, so an attempt
/// that lands a few seconds before the broker is ready stalls the whole service for another 30: the backlog took 51-99 s
/// to clear after recovery in four recorded runs. This puts the same filters on a 1-5 s schedule.
/// </para>
/// <para>
/// The policy has no setter, so it is replaced through the host configuration's backing field - once, during
/// configuration, before the bus starts. If MassTransit's internals ever differ, nothing is changed: the service starts
/// with the default, logs that it did, and <c>BrokerReconnectTests</c> fails in CI.
/// </para>
/// </summary>
public static class BrokerReconnect
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>
    /// <c>Messaging:ReconnectQuickly</c> (env <c>Messaging__ReconnectQuickly</c>), true unless set false: the way back to
    /// MassTransit's own schedule without a release - for an A/B measurement (specs/154 research D4), or if the short
    /// schedule ever misbehaves against a real broker.
    /// </summary>
    public const string Setting = "Messaging:ReconnectQuickly";

    /// <summary>
    /// What RabbitMqHostConfiguration's own policy retries and ignores - only the schedule differs: from 1 s, doubling,
    /// at most 5 s apart, with 1 s of jitter so endpoints and instances do not retry in step.
    /// </summary>
    public static readonly IRetryPolicy Policy = Retry.CreatePolicy(x =>
    {
        x.Handle<ConnectionException>();
        x.Handle<AlreadyClosedException>();
        x.Handle<EndOfStreamException>();
        x.Handle<OperationInterruptedException>(exception => exception.ChannelShouldBeClosed());
        x.Handle<NotSupportedException>(exception => exception.Message.Contains("Pipelining of requests forbidden"));
        // Wrong credentials are not a broker coming back; a fast loop of failed sign-ins helps nobody.
        x.Ignore<AuthenticationFailureException>();
        x.Exponential(1000, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
    });

    /// <summary>
    /// Call first thing in <c>UsingRabbitMq</c>. Returns whether the policy is now <see cref="Policy"/>. When it could
    /// not be applied, MassTransit's default stays and a warning is logged through <paramref name="services"/>; when
    /// <see cref="Setting"/> is false, the default stays on purpose and that is logged too.
    /// </summary>
    public static bool ReconnectQuickly(this IRabbitMqBusFactoryConfigurator configurator, IServiceProvider? services = null)
    {
        var logger = services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(BrokerReconnect));
        if (services?.GetService<IConfiguration>()?.GetValue(Setting, true) == false)
        {
            logger?.LogInformation("{Setting} is false: reconnecting to RabbitMQ on MassTransit's default schedule, up to 30 s apart.", Setting);
            return false;
        }

        var host = configurator.GetType().GetField("_hostConfiguration", Private)?.GetValue(configurator);
        var policy = host?.GetType().GetField("<ReceiveTransportRetryPolicy>k__BackingField", Private);
        if (host is null || policy is null || policy.FieldType != typeof(IRetryPolicy))
        {
            logger?.LogWarning("Could not shorten the RabbitMQ reconnect interval (specs/154): MassTransit's host configuration "
                    + "has changed. Reconnecting on its default schedule, up to 30 s apart.");
            return false;
        }

        policy.SetValue(host, Policy);
        return true;
    }

    /// <summary>The policy the configurator's host will reconnect with - for the tests.</summary>
    public static IRetryPolicy? CurrentPolicy(IRabbitMqBusFactoryConfigurator configurator)
    {
        var host = configurator.GetType().GetField("_hostConfiguration", Private)?.GetValue(configurator);
        return host?.GetType().GetProperty("ReceiveTransportRetryPolicy")?.GetValue(host) as IRetryPolicy;
    }
}
