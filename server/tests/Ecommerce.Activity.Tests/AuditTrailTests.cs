using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// The publisher's half (specs/041 research D3): what every service's <see cref="IAuditTrail"/> puts on the
/// wire - the actor from the token, the service's name, and snapshots already redacted.
/// </summary>
public class AuditTrailTests
{
    [Fact]
    public async Task An_entry_names_the_caller_by_their_most_powerful_role()
    {
        var user = new FakeUser { Id = Guid.NewGuid(), Email = "boss@demo.test", Roles = { "Customer", "Admin" } };
        await using var provider = Build(user);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAuditTrail>().RecordAsync(
                AuditCategory.Catalog, "ProductUpdated", "Product", "p-1", "Renamed",
                before: new { Name = "A", ApiToken = "t" }, after: new { Name = "B", ApiToken = "t" });
        }

        var sent = (await harness.Published.SelectAsync<AuditEntryRecorded>().First()).Context.Message;
        Assert.Equal(user.Id, sent.ActorId);
        Assert.Equal("boss@demo.test", sent.ActorEmail);
        Assert.Equal("Admin", sent.ActorRole);
        Assert.Equal("catalog", sent.Service);
        Assert.DoesNotContain("\"t\"", sent.Before);
        Assert.Contains("\"B\"", sent.After);
    }

    [Fact]
    public async Task Nobody_signed_in_is_the_system_unless_an_actor_is_given()
    {
        await using var provider = Build(new FakeUser());
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var who = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var trail = scope.ServiceProvider.GetRequiredService<IAuditTrail>();
            await trail.RecordAsync(AuditCategory.System, "Swept", "Reservation", null, "Swept");
            await trail.RecordAsync(AuditCategory.Security, "SignedIn", "User", who.ToString(), "Signed in",
                actor: new AuditActor(who, "lan@demo.test", "Customer"));
        }

        var sent = harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).ToList();
        Assert.Null(sent.Single(s => s.Action == "Swept").ActorId);
        Assert.Equal(who, sent.Single(s => s.Action == "SignedIn").ActorId);
    }

    private static ServiceProvider Build(FakeUser user)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(user);
        services.AddAuditTrail("catalog");
        services.AddMassTransitTestHarness();
        return services.BuildServiceProvider(true);
    }

    private sealed class FakeUser : ICurrentUser
    {
        public Guid? Id { get; set; }
        public string? Email { get; set; }
        public HashSet<string> Roles { get; } = [];
        public bool IsAuthenticated => Id is not null;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
