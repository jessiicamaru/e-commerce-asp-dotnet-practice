using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.SignInThrottling;
using Ecommerce.Contracts.Activity;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Wrong passwords counted per email (specs/062, #105): after 5 within 15 minutes, every sign-in for that
/// email waits 5 minutes - an unknown address exactly like a real one (#28). And reset links: one email a
/// minute per address.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class SignInThrottleTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task Five_wrong_passwords_pause_the_email_even_for_the_right_one()
    {
        var email = await PersonAsync();

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new LoginCommand(email, "wrong")));
        }

        var paused = await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(new LoginCommand(email, Password)));
        Assert.InRange(paused.RetryAfter, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    /// <summary>#28: the pause must not tell a stranger which emails have accounts.</summary>
    [Fact]
    public async Task An_unknown_email_is_answered_exactly_like_a_real_one()
    {
        var email = $"nobody-{Guid.NewGuid():N}@example.test";

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new LoginCommand(email, "wrong")));
        }

        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(new LoginCommand(email, "wrong")));
    }

    [Fact]
    public async Task Any_spelling_of_the_email_counts_against_the_same_address()
    {
        var email = await PersonAsync();

        for (var i = 0; i < 5; i++)
        {
            var spelling = i % 2 == 0 ? email.ToUpperInvariant() : $"  {email} ";
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new LoginCommand(spelling, "wrong")));
        }

        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(new LoginCommand(email, Password)));
    }

    /// <summary>All counted, however they arrive - and exactly one of them starts the pause.</summary>
    [Fact]
    public async Task Simultaneous_wrong_passwords_are_all_counted_and_pause_once()
    {
        var (email, id) = await PersonWithIdAsync();
        await using var provider = _fixture.For(Guid.Empty);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var scope = provider.CreateAsyncScope();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scope.ServiceProvider.GetRequiredService<ISender>().Send(new LoginCommand(email, "wrong")));
        }));

        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(new LoginCommand(email, Password)));
        var throttled = harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.Action == "SignInThrottled" && e.SubjectId == id.ToString()).ToList();
        var entry = Assert.Single(throttled);
        Assert.Equal("Security", entry.Category);
    }

    [Fact]
    public async Task Pausing_an_unknown_email_records_nothing()
    {
        var email = $"nobody-{Guid.NewGuid():N}@example.test";
        await using var provider = _fixture.For(Guid.Empty);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        for (var i = 0; i < 5; i++)
        {
            await using var scope = provider.CreateAsyncScope();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                scope.ServiceProvider.GetRequiredService<ISender>().Send(new LoginCommand(email, "wrong")));
        }

        Assert.DoesNotContain(harness.Published.Select<AuditEntryRecorded>(), x => x.Context.Message.Action == "SignInThrottled");
    }

    [Fact]
    public async Task The_right_password_starts_the_count_again()
    {
        var email = await PersonAsync();

        await WrongAsync(email, 4);
        await SendAsync(new LoginCommand(email, Password));
        await WrongAsync(email, 4);

        await SendAsync(new LoginCommand(email, Password));
    }

    [Fact]
    public async Task A_password_reset_starts_the_count_again()
    {
        var (email, id) = await PersonWithIdAsync();
        await WrongAsync(email, 5);

        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var token = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            (await WithDbAsync(db => db.OutgoingEmails.AsNoTracking().SingleAsync(e => e.RecipientId == id && e.Template == "PasswordReset"))).DataJson)!["token"];
        await SendAsync(new ResetPasswordCommand(token, "N3w-Passw0rd!"));

        await SendAsync(new LoginCommand(email, "N3w-Passw0rd!"));
    }

    [Fact]
    public async Task The_pause_ends()
    {
        var email = await PersonAsync();
        await WrongAsync(email, 5);

        await AgeAsync(email, pauseEnded: true, windowAgo: TimeSpan.Zero);

        await SendAsync(new LoginCommand(email, Password));
    }

    /// <summary>After a pause the count starts from nothing: one more wrong password does not pause again.</summary>
    [Fact]
    public async Task After_a_pause_a_full_run_of_wrong_passwords_is_needed_again()
    {
        var email = await PersonAsync();
        await WrongAsync(email, 5);
        await AgeAsync(email, pauseEnded: true, windowAgo: TimeSpan.Zero);

        await WrongAsync(email, 4);

        await SendAsync(new LoginCommand(email, Password));
    }

    [Fact]
    public async Task Wrong_passwords_older_than_the_window_are_forgotten()
    {
        var email = await PersonAsync();
        await WrongAsync(email, 4);
        await AgeAsync(email, pauseEnded: false, windowAgo: TimeSpan.FromMinutes(16));

        await WrongAsync(email, 4);

        await SendAsync(new LoginCommand(email, Password));
    }

    [Fact]
    public async Task Stale_counts_are_purged_and_running_ones_kept()
    {
        var stale = $"stale-{Guid.NewGuid():N}@example.test";
        var recent = $"recent-{Guid.NewGuid():N}@example.test";
        var paused = $"paused-{Guid.NewGuid():N}@example.test";
        await WrongAsync(stale, 1);
        await WrongAsync(recent, 1);
        await WrongAsync(paused, 5);
        await AgeAsync(stale, pauseEnded: false, windowAgo: TimeSpan.FromMinutes(16));
        await AgeAsync(paused, pauseEnded: false, windowAgo: TimeSpan.FromMinutes(16));

        await WithDbAsync(async db =>
        {
            var throttle = new Ecommerce.Infrastructure.Persistence.Repositories.SignInThrottleRepository(
                db, Microsoft.Extensions.Options.Options.Create(new SignInOptions()));
            return await throttle.PurgeStaleAsync(DateTime.UtcNow);
        });

        var left = await WithDbAsync(db => db.SignInThrottles.AsNoTracking()
            .Where(t => new[] { stale, recent, paused }.Contains(t.EmailKey)).Select(t => t.EmailKey).ToListAsync());
        Assert.Equal(new[] { paused, recent }.Order(), left.Order());
    }

    // ------------------------------------------------------------------ reset links: one a minute

    [Fact]
    public async Task Asking_for_a_link_twice_within_a_minute_sends_one_email()
    {
        var (email, id) = await PersonWithIdAsync();

        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        await SendAsync(new ForgotPasswordCommand(email, "vi"));   // the same answer, and nothing sent

        Assert.Equal(1, await ResetEmailsAsync(id));
    }

    [Fact]
    public async Task Asking_for_a_link_many_times_at_once_sends_one_email()
    {
        var (email, id) = await PersonWithIdAsync();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(new ForgotPasswordCommand(email, "vi"))));

        Assert.Equal(1, await ResetEmailsAsync(id));
    }

    [Fact]
    public async Task A_minute_later_a_new_link_can_be_asked_for()
    {
        var (email, id) = await PersonWithIdAsync();
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        await WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, DateTime.UtcNow.AddMinutes(-2))));

        await SendAsync(new ForgotPasswordCommand(email, "vi"));

        Assert.Equal(2, await ResetEmailsAsync(id));
    }

    // ------------------------------------------------------------------ helpers

    private async Task WrongAsync(string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new LoginCommand(email, "wrong")));
        }
    }

    /// <summary>Moves a count back in time: the pause over (or not), the window opened <paramref name="windowAgo"/>.</summary>
    private Task AgeAsync(string email, bool pauseEnded, TimeSpan windowAgo) =>
        WithDbAsync(db => db.SignInThrottles.Where(t => t.EmailKey == email.Trim().ToLowerInvariant())
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.WindowStartedAt, t => windowAgo == TimeSpan.Zero ? t.WindowStartedAt : DateTime.UtcNow - windowAgo)
                .SetProperty(t => t.BlockedUntil, t => pauseEnded ? DateTime.UtcNow.AddSeconds(-1) : t.BlockedUntil)));

    private Task<int> ResetEmailsAsync(Guid id) =>
        WithDbAsync(db => db.OutgoingEmails.CountAsync(e => e.RecipientId == id && e.Template == "PasswordReset"));

    private async Task<string> PersonAsync() => (await PersonWithIdAsync()).Email;

    private async Task<(string Email, Guid Id)> PersonWithIdAsync()
    {
        var email = $"throttle-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(new RegisterCommand(email, Password, "Lan", "Pham"));
        return (email, registered.Id);
    }

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task SendAsync(IRequest request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
