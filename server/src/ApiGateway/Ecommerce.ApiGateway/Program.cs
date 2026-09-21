using Ecommerce.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add YARP Reverse Proxy services and load configuration
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

// Every request starts a fresh trace HERE: a traceparent sent by a client is ignored, so no caller can
// choose a trace id or attach to someone else's checkout. Services behind the gateway propagate
// normally (feature 013, research D3).
builder.Services.AddSingleton<System.Diagnostics.DistributedContextPropagator>(new IgnoreIncomingTraceContextPropagator());
builder.AddObservability("gateway");

var app = builder.Build();

app.MapHealthChecks("/health");

// Enable YARP Reverse Proxy middleware routing
app.MapReverseProxy();

// Honour ASPNETCORE_URLS when the environment sets it - a container must bind 0.0.0.0, not
// localhost, or nothing outside it can connect however the ports are published. Falling back to
// the pinned address rather than dropping the argument keeps start-dev.sh working: with no
// argument every service would default to the same port and collide.
var listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

if (string.IsNullOrWhiteSpace(listenUrls))
{
    app.Run("http://localhost:5000");
}
else
{
    app.Run();
}

