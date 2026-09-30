using Ecommerce.Application.Email;
using Ecommerce.Contracts.Activity;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Notifications;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Administrators are told when an email fails for good (specs/115, #222): one digest notice per administrator, at most
/// one an hour, each failure counted in exactly one - also by two sweeps at once - and again after a retry.
/// </summary>
/// <remarks>
/// The dispatcher sweeps the whole table, which other tests share. So each test runs on its own hours of a clock a year
/// ahead - no other test's alert falls inside its quiet hour - and starts by marking every earlier failure counted and
/// every pending email sent: what is counted here is what the test made.
/// </remarks>
[Collection(nameof(IdentityTestCollection))]
public class EmailFailureAlertTests
{
    private static readonly DateTime Base = DateTime.UtcNow.AddYears(1);
    private static int _slot;

    private readonly IdentityTestFixture _fixture;
    private readonly DateTime _now;

    public EmailFailureAlertTests(IdentityTestFixture fixture)
    {
        _fixture = fixture;
        _now = Base.AddHours(10 * Interlocked.Increment(ref _slot));
        ResetAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task A_failure_tells_each_administrator_once_and_nobody_else()
    {
        var (first, second, moderator) = (await PersonAsync("Admin"), await PersonAsync("Admin"), await PersonAsync("Moderator"));
        var email = await BadEmailAsync();

        var notices = await DispatchAsync(_now);

        Assert.Equal(OutgoingEmailStatus.Failed, (await RowAsync(email)).Status);
        foreach (var admin in new[] { first, second })
        {
            var notice = Assert.Single(notices, n => n.RecipientId == admin);
            Assert.Equal(("1", "/admin/email-delivery"), (notice.Data["failed"], notice.Link));
        }

        Assert.DoesNotContain(notices, n => n.RecipientId == moderator);
        Assert.NotNull((await RowAsync(email)).FailureAlertedAt);
    }

    [Fact]
    public async Task Within_the_hour_nobody_is_told_again_and_the_next_notice_counts_what_waited()
    {
        var admin = await PersonAsync("Admin");
        await BadEmailAsync();
        Assert.Equal("1", Assert.Single(await DispatchAsync(_now), n => n.RecipientId == admin).Data["failed"]);

        var waiting = await BadEmailAsync();
        Assert.DoesNotContain(await DispatchAsync(_now.AddMinutes(30)), n => n.RecipientId == admin);
        Assert.Null((await RowAsync(waiting)).FailureAlertedAt);

        await BadEmailAsync();
        var later = Assert.Single(await DispatchAsync(_now.AddMinutes(61)), n => n.RecipientId == admin);
        Assert.Equal("2", later.Data["failed"]);
        Assert.NotNull((await RowAsync(waiting)).FailureAlertedAt);
    }

    [Fact]
    public async Task A_retried_email_that_fails_again_is_counted_again()
    {
        var admin = await PersonAsync("Admin");
        var email = await BadEmailAsync();
        await DispatchAsync(_now);

        await using (var scope = _fixture.For(Guid.Empty).CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IOutgoingEmailRepository>().TryRetryAsync(email, _now.AddHours(2)));
        }

        Assert.Null((await RowAsync(email)).FailureAlertedAt);
        var again = Assert.Single(await DispatchAsync(_now.AddHours(2)), n => n.RecipientId == admin);
        Assert.Equal("1", again.Data["failed"]);
    }

    /// <summary>Two instances sweeping at once: each failure is in exactly one notice, whichever instance sends it.</summary>
    [Fact]
    public async Task Two_sweeps_at_once_count_each_failure_once()
    {
        var admin = await PersonAsync("Admin");
        for (var i = 0; i < 6; i++) await BadEmailAsync();

        var both = await Task.WhenAll(DispatchAsync(_now), DispatchAsync(_now));

        var counted = both.SelectMany(n => n).Where(n => n.RecipientId == admin).Sum(n => int.Parse(n.Data["failed"]));
        Assert.Equal(6, counted);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Everything other tests left is out of the way: their failures counted long ago, their queue sent.</summary>
    private async Task ResetAsync()
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var longAgo = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.OutgoingEmails.Where(e => e.Status == OutgoingEmailStatus.Failed && e.FailureAlertedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.FailureAlertedAt, longAgo));
        await db.OutgoingEmails.Where(e => e.Status == OutgoingEmailStatus.Pending)
            .ExecuteUpdateAsync(x => x.SetProperty(e => e.Status, OutgoingEmailStatus.Sent));
    }

    private async Task<Guid> PersonAsync(string role)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Id = Guid.CreateVersion7(), Email = $"alert-{Guid.NewGuid():N}@example.test", FirstName = role, LastName = "Test",
            PasswordHash = "x", Roles = [await db.Roles.SingleAsync(r => r.Name == role)],
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>An email to nobody: the dispatcher fails it at once, since retrying will not create a recipient.</summary>
    private async Task<Guid> BadEmailAsync()
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = new OutgoingEmail
        {
            Id = Guid.CreateVersion7(), RecipientId = Guid.CreateVersion7(), Template = "OrderPaid", DataJson = "{}",
            Language = "en", CreatedAt = _now, NextAttemptAt = _now.AddYears(-2),
        };
        db.OutgoingEmails.Add(email);
        await db.SaveChangesAsync();
        return email.Id;
    }

    private async Task<OutgoingEmail> RowAsync(Guid id)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutgoingEmails.AsNoTracking().SingleAsync(e => e.Id == id);
    }

    /// <summary>One sweep at <paramref name="now"/>, and the "emails failed" notices it published.</summary>
    private async Task<List<UserNotificationRequested>> DispatchAsync(DateTime now)
    {
        await using var provider = _fixture.For(Guid.Empty);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DispatchEmailsCommand(now));
        }

        return harness.Published.Select<UserNotificationRequested>().Select(x => x.Context.Message)
            .Where(n => n.Kind == NotificationKind.EmailsFailed).ToList();
    }
}
