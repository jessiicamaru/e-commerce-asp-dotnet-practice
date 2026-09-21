using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Shared.Observability;

/// <summary>
/// Puts the message's <c>OrderId</c> on every log line and span produced while consuming it.
/// </summary>
/// <remarks>
/// <para>
/// "Show me everything about order X" is one Seq query - <c>OrderId = 'X'</c> - only if every service
/// writes <c>OrderId</c> on the lines it logs for that order, including lines written deep inside a
/// handler that knows nothing about logging scopes. A consume filter does it once, for every consumer
/// and the saga, instead of in each of them (feature 013).
/// </para>
/// <para>
/// Every contract that concerns an order names its id <c>OrderId</c> (a <c>Guid</c>). Messages without
/// one - product and stock events - pass through untouched. The property lookup is reflected once per
/// message type and cached.
/// </para>
/// </remarks>
public class OrderIdLogScopeFilter<T>(ILogger<OrderIdLogScopeFilter<T>> logger) : IFilter<ConsumeContext<T>>
    where T : class
{
    private static readonly PropertyInfo? OrderIdProperty = Lookup.For(typeof(T));

    private readonly ILogger<OrderIdLogScopeFilter<T>> _logger = logger;

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        if (OrderIdProperty?.GetValue(context.Message) is not Guid orderId)
        {
            await next.Send(context);
            return;
        }

        Activity.Current?.SetTag("order.id", orderId.ToString());

        using (_logger.BeginScope(new Dictionary<string, object> { ["OrderId"] = orderId }))
        {
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("orderIdLogScope");

    private static class Lookup
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo?> Cache = new();

        public static PropertyInfo? For(Type type) => Cache.GetOrAdd(type, t =>
        {
            var p = t.GetProperty("OrderId", BindingFlags.Public | BindingFlags.Instance);
            return p?.PropertyType == typeof(Guid) ? p : null;
        });
    }
}
