using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecommerce.ApiGateway;

/// <summary>
/// How fast one client may call the anonymous auth endpoints (specs/062, #105). Three fixed-window
/// policies, each counted per client IP, attached to routes through YARP's <c>RateLimiterPolicy</c>:
/// <list type="bullet">
/// <item><c>sign-in</c> - login, register, register-seller, reset-password (30 a minute);</item>
/// <item><c>email</c> - forgot-password, which sends a real email (5 a minute);</item>
/// <item><c>session</c> - refresh, looser because two tabs share one cookie (60 a minute);</item>
/// <item><c>views</c> - <c>POST /api/products/{id}/view</c>, the one anonymous write that feeds a number people read
/// (specs/086, #173; 30 a minute - a shopper opens nothing like that many product pages).</item>
/// </list>
/// Each is <c>RateLimits:&lt;policy&gt;:PermitLimit</c> / <c>WindowSeconds</c>. Counted in memory, per
/// gateway instance: a restart forgives, which costs nothing for limits about one client's pace.
/// </summary>
public static class AuthRateLimits
{
    public const string SignIn = "sign-in";
    public const string Email = "email";
    public const string Session = "session";
    public const string Views = "views";

    private static readonly (string Name, int PermitLimit, int WindowSeconds)[] Defaults =
    [
        (SignIn, 30, 60),
        (Email, 5, 60),
        (Session, 60, 60),
        (Views, 30, 60),
    ];

    public static IServiceCollection AddAuthRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var policies = Defaults.Select(d => (
            d.Name,
            PermitLimit: configuration.GetValue($"RateLimits:{d.Name}:PermitLimit", d.PermitLimit),
            WindowSeconds: configuration.GetValue($"RateLimits:{d.Name}:WindowSeconds", d.WindowSeconds))).ToList();

        // A zero or negative limit would silently switch the protection off - refuse to start instead.
        var problems = policies
            .Where(p => p.PermitLimit < 1 || p.WindowSeconds < 1)
            .Select(p => $"RateLimits:{p.Name} needs PermitLimit and WindowSeconds of at least 1 (was {p.PermitLimit} / {p.WindowSeconds}).")
            .ToList();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", problems));
        }

        services.AddRateLimiter(options =>
        {
            foreach (var policy in policies)
            {
                options.AddPolicy(policy.Name, context => RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
            }

            options.OnRejected = WriteRefusalAsync;
        });

        return services;
    }

    /// <summary>
    /// Believes <c>X-Forwarded-For</c> only from the proxies in <c>GATEWAY_TRUSTED_PROXIES</c> - IPs or CIDRs,
    /// comma-separated. Nothing configured: nobody is trusted and the header is ignored, because anybody can
    /// write it, and a limit keyed on a header the caller writes is no limit at all.
    /// </summary>
    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration["GATEWAY_TRUSTED_PROXIES"] ?? string.Empty;
        var proxies = new List<IPAddress>();
        var networks = new List<System.Net.IPNetwork>();

        foreach (var entry in configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/') && System.Net.IPNetwork.TryParse(entry, out var network))
            {
                networks.Add(network);
            }
            else if (IPAddress.TryParse(entry, out var address))
            {
                proxies.Add(address);
            }
            else
            {
                throw new InvalidOperationException($"GATEWAY_TRUSTED_PROXIES: '{entry}' is neither an IP address nor a CIDR network.");
            }
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // ⚠️ Empty KnownProxies AND KnownIPNetworks does not mean "trust nobody" - the middleware then
            // trusts EVERY peer (the documented way to forward from anyone). So with nothing configured the
            // header is not read at all; a test forging a new address per request is what found this.
            options.ForwardedHeaders = proxies.Count + networks.Count == 0
                ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor;
            // One hop: the address our trusted proxy saw. Anything further left was written by the client.
            options.ForwardLimit = 1;
            // The defaults trust loopback; here nothing is trusted unless configured.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            proxies.ForEach(options.KnownProxies.Add);
            networks.ForEach(options.KnownIPNetworks.Add);
        });

        return services;
    }

    /// <summary>The client's address, as the connection says - or as a trusted proxy said, once forwarded headers ran.</summary>
    private static string ClientKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

    /// <summary>429 as ProblemDetails, with <c>Retry-After</c> and the same seconds as <c>retryAfter</c>.</summary>
    private static async ValueTask WriteRefusalAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var seconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;

        var response = rejected.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = $"Too many attempts. Try again in {seconds} seconds.",
            Instance = rejected.HttpContext.Request.Path,
        };
        problem.Extensions["retryAfter"] = seconds;

        await response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
    }
}
