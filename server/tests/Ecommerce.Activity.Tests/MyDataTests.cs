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

    /// <summary>
    /// specs/112: notices go; the audit entries stay, without the person's email - as an actor or in a summary - and
    /// without the snapshots of their own details. Nobody else's entries move.
    /// </summary>
    [Fact]
    public async Task A_deleted_account_leaves_the_record_of_what_happened_without_the_person()
    {
        var mai = Guid.CreateVersion7();
        var lan = Guid.CreateVersion7();
        var moderator = Guid.CreateVersion7();
        const string maiEmail = "mai.erased@example.test";
        await SeedAsync(db =>
        {
            db.Notifications.Add(Notice(mai, "mai-notice"));
            db.Notifications.Add(Notice(lan, "lan-notice"));
            db.AuditEntries.Add(Entry(actor: mai, about: null, $"{maiEmail} changed their details", before: "{\"FirstName\":\"Mai\"}", actorEmail: maiEmail));
            db.AuditEntries.Add(Entry(actor: moderator, about: mai, $"Locked {maiEmail} for 3 days", before: null, actorEmail: "mod@example.test"));
            db.AuditEntries.Add(Entry(actor: lan, about: null, "lan@example.test changed their details", before: "{\"FirstName\":\"Lan\"}", actorEmail: "lan@example.test"));
            return db.SaveChangesAsync();
        });

        await using (var scope = _fixture.NewScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new EraseAccountCommand(mai, maiEmail));
            await sender.Send(new EraseAccountCommand(mai, maiEmail));   // a redelivery changes nothing
        }

        _fixture.CurrentUser.Id = mai;
        await using var read = _fixture.NewScope();
        var export = await read.ServiceProvider.GetRequiredService<ISender>().Send(new GetMyDataQuery());
        foreach (var section in ActivityPersonalData.Inventory.Erased)
            Assert.Empty(export.Sections[section]);
        Assert.Equal(2, export.Sections["activity"].Count);

        var db = read.ServiceProvider.GetRequiredService<ActivityDbContext>();
        var entries = await db.AuditEntries.AsNoTracking().Where(e => e.ActorId == mai || e.AboutUserId == mai).ToListAsync();
        Assert.All(entries, e => Assert.DoesNotContain(maiEmail, e.Summary));
        Assert.Contains(entries, e => e.Summary == "Locked a deleted account for 3 days");
        var own = Assert.Single(entries, e => e.ActorId == mai);
        Assert.Null(own.ActorEmail);
        Assert.Null(own.Before);
        Assert.Equal("[]", own.Changes);
        Assert.Equal("mod@example.test", Assert.Single(entries, e => e.ActorId == moderator).ActorEmail);   // staff stay named in the record

        var lanEntry = await db.AuditEntries.AsNoTracking().SingleAsync(e => e.ActorId == lan);
        Assert.Equal("lan@example.test", lanEntry.ActorEmail);
        Assert.NotNull(lanEntry.Before);
        Assert.True(await db.Notifications.AnyAsync(n => n.RecipientId == lan));
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
