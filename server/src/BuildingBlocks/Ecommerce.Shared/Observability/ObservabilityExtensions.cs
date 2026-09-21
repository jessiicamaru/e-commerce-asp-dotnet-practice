using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Ecommerce.Shared.Observability;

/// <summary>
/// Structured logs and distributed traces for one service, shipped over OTLP (feature 013).
/// </summary>
/// <remarks>
/// <para>
/// <b>One checkout is one trace.</b> ASP.NET Core, HttpClient (and therefore the gRPC clients) and
/// MassTransit all emit <see cref="Activity"/>s and propagate W3C <c>traceparent</c> across their hops -
/// MassTransit carries it in message headers - so the trace survives the broker, which is where six
/// services stop being one call stack. Every log line written inside a span carries its trace id.
/// </para>
/// <para>
/// <b>Where it goes:</b> <c>OTLP_ENDPOINT</c> (e.g. <c>http://localhost:5341/ingest/otlp</c> for Seq),
/// with <c>/v1/logs</c> and <c>/v1/traces</c> appended; <c>OTLP_API_KEY</c> is sent as Seq's
/// <c>X-Seq-ApiKey</c> header when set. When <c>OTLP_ENDPOINT</c> is unset, nothing is exported and the
/// service behaves exactly as before - telemetry is optional, so CI and a bare <c>dotnet run</c> need
/// no collector. That is a deliberate exception to failing fast on missing configuration: losing
/// telemetry must never stop checkout.
/// </para>
/// <para>
/// <b>What is never recorded:</b> request and response bodies, headers (so no bearer token), and
/// database parameter values. None of the instrumentation used here captures them by default, and
/// nothing below turns that on - see specs/013-observability research D5 before changing that.
/// </para>
/// </remarks>
public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder, string serviceName)
    {
        var endpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT")?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return builder;
        }

        var apiKey = Environment.GetEnvironmentVariable("OTLP_API_KEY");

        void Configure(OtlpExporterOptions o, string signal)
        {
            o.Endpoint = new Uri($"{endpoint}/v1/{signal}");
            o.Protocol = OtlpExportProtocol.HttpProtobuf;

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                o.Headers = $"X-Seq-ApiKey={apiKey}";
            }
        }

        var resource = ResourceBuilder.CreateDefault().AddService($"ecommerce-{serviceName}");

        builder.Logging.AddOpenTelemetry(o =>
        {
            o.SetResourceBuilder(resource);
            o.IncludeScopes = true;          // OrderId arrives as a scope - see OrderIdLogScopeFilter
            o.IncludeFormattedMessage = true;
            o.ParseStateValues = true;       // structured properties, queryable as properties in Seq
            o.AddOtlpExporter(e => Configure(e, "logs"));
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService($"ecommerce-{serviceName}"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o =>
                {
                    // Health probes run every few seconds from Docker; tracing them buries checkouts.
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddSource("MassTransit")   // publish, send, consume and saga spans, across the broker
                .AddSource("Npgsql")        // one span per database command - statements, never parameters
                .AddOtlpExporter(e => Configure(e, "traces")));

        return builder;
    }
}
