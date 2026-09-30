using System.Text.Json;
using Ecommerce.Activity.Application.MyData;
using Ecommerce.Activity.Domain.Entities;
using Ecommerce.Activity.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// What Activity holds about a person, handed to them (#217, specs/111): their notices, what they did and what was
/// decided about them - never who decided it, and never a snapshot, which can hold somebody else's data.
/// </summary>
[Collection(nameof(ActivityTestCollection))]
public class MyDataTests(ActivityTestFixture fixture) : IDisposable
{
    private readonly ActivityTestFixture _fixture = fixture;

    public void Dispose()
    {
        _fixture.CurrentUser.Id = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Every_table_of_the_model_is_declared_exported_withheld_or_not_personal()
    {
        await using var scope = _fixture.NewScope();
        var model = scope.ServiceProvider.GetRequiredService<ActivityDbContext>().Model;

        Assert.Empty(ActivityPersonalData.Inventory.Problems(model.GetEntityTypes().Select(e => e.GetTableName()!)));
    }

    [Fact]
    public async Task A_person_gets_their_notices_their_actions_and_decisions_about_them_without_the_staff_or_snapshots()
    {
        var mai = Guid.CreateVersion7();
        var lan = Guid.CreateVersion7();
        var moderator = Guid.CreateVersion7();
        await SeedAsync(db =>
        {
            db.Notifications.Add(Notice(mai, "mai-notice"));
            db.Notifications.Add(Notice(lan, "lan-notice"));
            db.AuditEntries.Add(Entry(actor: mai, about: null, "Mai changed her name", before: "{\"secret\":\"mai-before\"}"));
            db.AuditEntries.Add(Entry(actor: moderator, about: mai, "Account locked", before: "{\"other\":\"lan-data\"}", actorEmail: "mod@example.test"));
            db.AuditEntries.Add(Entry(actor: lan, about: null, "Lan changed her name", before: null));
            return db.SaveChangesAsync();
        });
        _fixture.CurrentUser.Id = mai;

        await using var scope = _fixture.NewScope();
        var export = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMyDataQuery());

        Assert.Equal("activity", export.Service);
        Assert.Single(export.Sections["notifications"]);
        Assert.Equal(2, export.Sections["activity"].Count);
        var json = JsonSerializer.Serialize(export);
        Assert.Contains("mai-notice", json);
        Assert.Contains("Mai changed her name", json);
        Assert.Contains("Account locked", json);
        Assert.DoesNotContain("lan-notice", json);
        Assert.DoesNotContain("Lan changed her name", json);
        Assert.DoesNotContain(moderator.ToString(), json);
        Assert.DoesNotContain("mod@example.test", json);
        Assert.DoesNotContain("mai-before", json);
        Assert.DoesNotContain("lan-data", json);
    }

    private static Notification Notice(Guid recipient, string marker) => new()
    {
        Id = Guid.CreateVersion7(), RecipientId = recipient, Kind = "OrderPaid", Data = $"{{\"order\":\"{marker}\"}}", CreatedAt = DateTime.UtcNow,
    };

    private static AuditEntry Entry(Guid actor, Guid? about, string summary, string? before, string? actorEmail = null) => new()
    {
        Id = Guid.CreateVersion7(), Category = "User", Action = "Changed", ActorId = actor, ActorEmail = actorEmail, ActorRole = "Moderator",
        SubjectType = "User", SubjectId = (about ?? actor).ToString(), Summary = summary, AboutUserId = about, Before = before,
        Changes = "[]", Service = "identity", OccurredAt = DateTime.UtcNow, RecordedAt = DateTime.UtcNow,
    };

    private async Task SeedAsync(Func<ActivityDbContext, Task> work)
    {
        await using var scope = _fixture.NewScope();
        await work(scope.ServiceProvider.GetRequiredService<ActivityDbContext>());
    }
}
