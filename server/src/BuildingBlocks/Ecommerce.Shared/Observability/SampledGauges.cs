using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Shared.Observability;

/// <summary>One reading of a sampled gauge: a value and the labels it carries.</summary>
public sealed record GaugeReading(double Value, params KeyValuePair<string, object?>[] Labels);

/// <summary>
/// The shop's own metrics, read from committed rows on a schedule (specs/148): orders by status, settle times, outbox
/// backlog.
/// </summary>
/// <remarks>
/// <para>
/// Gauges, never counters bumped in a handler. A handler runs inside its consume's transaction, which the transient retry
/// (specs/145) may roll back and run again: a counter there counts attempts, not orders. A gauge read from the database
/// says what is committed, whatever happened on the way.
/// </para>
/// <para>
/// This building block knows no database: each service passes its query, from the layer that does. Nothing is registered
/// unless <c>METRICS_ENDPOINT</c> is set - like every other signal (specs/013), metrics are optional and their absence never
/// costs a query.
/// </para>
/// </remarks>
public static class SampledGauges
{
    /// <summary>The meter the shop's own gauges are on; <see cref="ObservabilityExtensions"/> exports it.</summary>
    public const string MeterName = "Ecommerce";

    internal static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// A gauge whose readings come from <paramref name="sample"/>, run every <c>METRICS_SAMPLE_SECONDS</c> (15) in a scope
    /// of its own. A sample that throws keeps the previous readings and is logged - a metric never stops the service.
    /// </summary>
    public static IServiceCollection AddSampledGauge(
        this IServiceCollection services,
        string name,
        string unit,
        string description,
        Func<IServiceProvider, CancellationToken, Task<IReadOnlyList<GaugeReading>>> sample)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("METRICS_ENDPOINT")))
        {
            return services;
        }

        services.AddSingleton(new SampledGauge(name, unit, description, sample));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, GaugeSampler>());
        return services;
    }
}

/// <summary>One gauge: its latest readings, observed by the meter whenever it is collected.</summary>
public sealed class SampledGauge
{
    private IReadOnlyList<GaugeReading> _latest = [];

    public SampledGauge(
        string name,
        string unit,
        string description,
        Func<IServiceProvider, CancellationToken, Task<IReadOnlyList<GaugeReading>>> sample)
    {
        Name = name;
        Sample = sample;
        SampledGauges.Meter.CreateObservableGauge(
            name,
            () => _latest.Select(r => new Measurement<double>(r.Value, r.Labels)),
            unit,
            description);
    }

    public string Name { get; }

    public Func<IServiceProvider, CancellationToken, Task<IReadOnlyList<GaugeReading>>> Sample { get; }

    /// <summary>The readings the meter reports until the next sample.</summary>
    public IReadOnlyList<GaugeReading> Latest => _latest;

    internal void Record(IReadOnlyList<GaugeReading> readings) => _latest = readings;
}

/// <summary>Runs every sampled gauge's query on a schedule, each in its own scope.</summary>
public sealed class GaugeSampler(
    IEnumerable<SampledGauge> gauges,
    IServiceScopeFactory scopes,
    ILogger<GaugeSampler> logger) : BackgroundService
{
    private readonly IReadOnlyList<SampledGauge> _gauges = gauges.ToList();
    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly ILogger<GaugeSampler> _logger = logger;

    private static TimeSpan Interval =>
        TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("METRICS_SAMPLE_SECONDS"), out var s) && s > 0 ? s : 15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await SampleAllAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One round: every gauge read once. Public for the tests that hold it to committed rows.</summary>
    public async Task SampleAllAsync(CancellationToken cancellationToken)
    {
        foreach (var gauge in _gauges)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                gauge.Record(await gauge.Sample(scope.ServiceProvider, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Gauge {Gauge} could not be sampled; it keeps its previous readings.", gauge.Name);
            }
        }
    }
}
