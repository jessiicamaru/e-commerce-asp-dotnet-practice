using System.Diagnostics;

namespace Ecommerce.Shared.Observability;

/// <summary>
/// The gateway's propagator: it <b>ignores</b> any trace context a client sends, and passes its own on.
/// </summary>
/// <remarks>
/// <para>
/// Accepting <c>traceparent</c> from the outside world lets any caller choose a trace id - and so
/// collide two customers' checkouts into one trace on purpose, or attach their requests to someone
/// else's. The gateway is the edge, so every request starts a fresh trace there (specs/013 research
/// D3). Inside the system, services propagate normally; only the gateway is registered with this.
/// </para>
/// <para>
/// ASP.NET Core's hosting layer takes the <see cref="DistributedContextPropagator"/> from dependency
/// injection when it creates the request's activity, so registering this one is enough for the
/// incoming side. Outgoing, YARP and HttpClient inject through the default propagator, unchanged.
/// </para>
/// </remarks>
public sealed class IgnoreIncomingTraceContextPropagator : DistributedContextPropagator
{
    private static readonly DistributedContextPropagator Default = CreateDefaultPropagator();

    public override IReadOnlyCollection<string> Fields => Default.Fields;

    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter) =>
        Default.Inject(activity, carrier, setter);

    public override void ExtractTraceIdAndState(
        object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
    {
        traceId = null;
        traceState = null;
    }

    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(
        object? carrier, PropagatorGetterCallback? getter) => null;
}
