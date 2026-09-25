using System.Text.Json;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A 403's facts reach the response body (specs/049) - in Production, where every other exception's
/// detail is hidden - and never displace what the handler itself writes.
/// </summary>
public class ForbiddenProblemTests
{
    [Fact]
    public async Task A_refusal_carries_its_facts_beside_its_sentence_outside_Development()
    {
        var body = await HandleAsync(new ForbiddenException("This account is banned: Fraud",
            new Dictionary<string, object?> { ["code"] = "AccountBanned", ["reason"] = "Fraud" }));

        Assert.Equal(403, body.GetProperty("status").GetInt32());
        Assert.Equal("This account is banned: Fraud", body.GetProperty("detail").GetString());
        Assert.Equal("AccountBanned", body.GetProperty("code").GetString());
        Assert.Equal("Fraud", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_date_fact_is_ISO_8601_in_UTC()
    {
        var until = new DateTime(2026, 10, 1, 7, 30, 0, DateTimeKind.Utc);
        var body = await HandleAsync(new ForbiddenException("locked", new Dictionary<string, object?> { ["until"] = until }));

        Assert.Equal("2026-10-01T07:30:00Z", body.GetProperty("until").GetString());
    }

    [Fact]
    public async Task A_fact_cannot_hide_the_trace_id()
    {
        var body = await HandleAsync(new ForbiddenException("no", new Dictionary<string, object?> { ["traceId"] = "forged" }));

        Assert.Equal("the-trace", body.GetProperty("traceId").GetString());
    }

    /// <summary>specs/062: a 429 says how long to wait, in the header and the body, in whole seconds rounded up.</summary>
    [Fact]
    public async Task Too_many_requests_says_how_long_to_wait()
    {
        var context = NewContext();
        var body = await HandleAsync(new TooManyRequestsException("Too many wrong passwords for this email. Try again later.",
            TimeSpan.FromSeconds(299.2)), context);

        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Equal("Too many wrong passwords for this email. Try again later.", body.GetProperty("detail").GetString());
        Assert.Equal(300, body.GetProperty("retryAfter").GetInt32());
        Assert.Equal("300", context.Response.Headers.RetryAfter.ToString());
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "the-trace" };
        context.Request.Path = "/api/auth/login";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> HandleAsync(Exception exception, DefaultHttpContext? given = null)
    {
        var context = given ?? NewContext();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, new ProductionEnvironment());
        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement.Clone();
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Ecommerce.Identity.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
