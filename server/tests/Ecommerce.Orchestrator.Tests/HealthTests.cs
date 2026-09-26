using System.Net;
using System.Text.Json;
using Ecommerce.Orchestrator.WebApi.StateMachines;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Ecommerce.Orchestrator.Tests;

/// <summary>
/// The Orchestrator answers <c>/health</c> (specs/071, #115) - the one service that did not, so nothing could wait
/// for it or probe it. The real host, with the broker swapped for MassTransit's in-memory harness and the sweeper
/// taken out; the database check is the real one, against a PostgreSQL that is there and one that is not.
/// </summary>
public class HealthTests
{
    [Fact]
    public async Task Health_is_200_and_names_the_saga_database_and_the_broker()
    {
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "123456";
        await using var factory = Host($"Host=localhost;Port=5436;Database=postgres;Username=postgres;Password={password}");

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checks = await CheckNamesAsync(response);
        Assert.Contains("orchestrator_postgres_db", checks);
        Assert.Contains("masstransit-bus", checks);
    }

    /// <summary>The acceptance's other half: not 200 when what it depends on is gone.</summary>
    [Fact]
    public async Task Health_is_503_when_the_saga_database_cannot_be_reached()
    {
        await using var factory = Host("Host=localhost;Port=1;Database=postgres;Username=postgres;Password=x;Timeout=2");

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static WebApplicationFactory<Program> Host(string connection) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrchestratorDbContext>>();
            services.AddDbContext<OrchestratorDbContext>(options => options.UseNpgsql(connection));

            // No sweeper reading a saga table this database does not have, and no RabbitMQ: the harness keeps the
            // bus - and its "masstransit-bus" health check - in memory.
            services.RemoveAll<IHostedService>();
            services.AddMassTransitTestHarness();
        }));

    private static async Task<List<string>> CheckNamesAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();
    }
}
