using Ecommerce.Activity.Application.Audit.Commands;
using Ecommerce.Activity.Application.Audit.Queries;
using Ecommerce.Contracts.Activity;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// What staff see about a person before deciding (#198, specs/100): the moderation decisions about them, with the reason
/// given, and nothing from any other part of the log.
/// </summary>
[Collection(nameof(ActivityTestCollection))]
public class PersonHistoryTests(ActivityTestFixture fixture)
{
    private readonly ActivityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_moderator_sees_the_earlier_locks_and_why_newest_first()
    {
        var mai = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await RecordAsync(Entry("Moderation", "AccountLocked", "User", mai, """{"lockedUntil":"2026-09-01T00:00:00Z","lockReason":"Spam"}""", now.AddDays(-20)));
        await RecordAsync(Entry("Moderation", "AccountUnlocked", "User", mai, """{"lockedUntil":null,"lockReason":null}""", now.AddDays(-15)));
        await RecordAsync(Entry("Moderation", "AccountLocked", "User", mai, """{"lockedUntil":"2026-09-20T00:00:00Z","lockReason":"Abuse in reviews"}""", now.AddDays(-5)));
        await RecordAsync(Entry("Moderation", "ReviewHidden", "Review", mai, """{"hidden":true,"reason":"Advertising"}""", now.AddDays(-1)));
        // Somebody else's lock is not in Mai's history.
        await RecordAsync(Entry("Moderation", "AccountLocked", "User", Guid.NewGuid(), """{"lockReason":"Other"}""", now));

        var history = await SendAsync(new GetPersonModerationHistoryQuery(mai));

        Assert.Equal(4, history.TotalCount);
        Assert.Equal(
            [("ReviewHidden", "Advertising"), ("AccountLocked", "Abuse in reviews"), ("AccountUnlocked", null), ("AccountLocked", "Spam")],
            history.Items.Select(i => (i.Action, i.Reason)));
    }

    /// <summary>The route is staff's, not an administrator's: it must not become a way into the rest of the log.</summary>
    [Fact]
    public async Task Nothing_outside_moderation_is_shown()
    {
        var lan = Guid.NewGuid();
        await RecordAsync(Entry("Security", "SignedIn", "User", lan, null, DateTime.UtcNow));
        await RecordAsync(Entry("Order", "OrderCancelled", "Order", lan, """{"reason":"Changed my mind"}""", DateTime.UtcNow));
        await RecordAsync(Entry("User", "PasswordChanged", "User", lan, null, DateTime.UtcNow));
        await RecordAsync(Entry("Moderation", "ShopRejected", "ShopApplication", lan, """{"status":"Rejected","reason":"Tell us what you sell"}""", DateTime.UtcNow));

        var history = await SendAsync(new GetPersonModerationHistoryQuery(lan));

        var only = Assert.Single(history.Items);
        Assert.Equal(("ShopRejected", "Tell us what you sell"), (only.Action, only.Reason));
    }

    [Fact]
    public async Task A_person_with_no_history_has_an_empty_page()
    {
        var history = await SendAsync(new GetPersonModerationHistoryQuery(Guid.NewGuid()));
        Assert.Equal(0, history.TotalCount);
        Assert.Empty(history.Items);
    }

    // ------------------------------------------------------------------ helpers

    private static AuditEntryRecorded Entry(string category, string action, string subjectType, Guid about, string? after, DateTime at) =>
        new(Guid.CreateVersion7(), category, action, Guid.NewGuid(), "mod@demo.test", "Moderator",
            subjectType, subjectType == "User" ? about.ToString() : Guid.NewGuid().ToString(), $"{action} happened",
            null, after, "tests", at, about);

    private Task<bool> RecordAsync(AuditEntryRecorded entry) => SendAsync(new RecordAuditEntryCommand(entry));

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
