using System.Text.Json;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Email;
using Ecommerce.Contracts.Activity;
using Ecommerce.Infrastructure.Persistence;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A forgotten password (specs/061, #103): a link for the account's owner, sent by email, used once within 30
/// minutes - and the same answer for every address, so nobody learns which emails have accounts (#28).
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class PasswordResetTests(IdentityTestFixture fixture)
{
    private const string OldPassword = "Passw0rd!23";
    private const string NewPassword = "N3w-Passw0rd!";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task Asking_answers_the_same_for_anybody_and_only_a_real_account_gets_a_link()
    {
        var (id, email) = await PersonAsync();
        var before = await CountAsync();

        await SendAsync(new ForgotPasswordCommand($"nobody-{Guid.NewGuid():N}@example.test", "en"));   // completes, and nothing more
        Assert.Equal(before, await CountAsync());

        await SendAsync(new ForgotPasswordCommand(email.ToUpperInvariant(), "en"));   // any case, like sign-in (#49)
        var mail = Assert.Single(await PendingResetEmailsAsync(id));
        Assert.Equal("en", mail.Language);
    }

    [Fact]
    public async Task Only_the_hash_is_kept_and_the_link_carries_the_token()
    {
        var (id, email) = await PersonAsync();
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var token = await TokenAsync(id);

        var stored = await WithDbAsync(db => db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == id));
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(ResetTokens.Hash(token), stored.TokenHash);
        Assert.True(stored.ExpiresAt <= DateTime.UtcNow.AddMinutes(30));

        await SendAsync(new DispatchEmailsCommand(DateTime.UtcNow));
        var sent = Assert.Single(_fixture.Mail.SentTo(email), m => m.Subject == "Đặt lại mật khẩu của bạn");
        Assert.Contains($"http://shop.test/reset-password?token={Uri.EscapeDataString(token)}", sent.Body);

        // Delivered: the row keeps no copy of the secret (specs/061).
        var row = await WithDbAsync(db => db.OutgoingEmails.AsNoTracking().SingleAsync(e => e.RecipientId == id && e.Template == "PasswordReset"));
        Assert.DoesNotContain(token, row.DataJson);
    }

    [Fact]
    public async Task A_reset_changes_the_password_and_ends_every_session()
    {
        var (id, email) = await PersonAsync();
        var session = await SendAsync(new LoginCommand(email, OldPassword));
        await SendAsync(new ForgotPasswordCommand(email, "vi"));

        await SendAsync(new ResetPasswordCommand(await TokenAsync(id), NewPassword));

        await SendAsync(new LoginCommand(email, NewPassword));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new LoginCommand(email, OldPassword)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(new RefreshTokenCommand(session.RefreshToken)));
    }

    [Fact]
    public async Task A_link_works_once_and_not_after_it_expires_and_a_made_up_one_never_does()
    {
        var (id, email) = await PersonAsync();
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var used = await TokenAsync(id);
        await SendAsync(new ResetPasswordCommand(used, NewPassword));

        await RefusedAsync(used);
        await RefusedAsync("made-up-token");

        await AskedLongAgoAsync(id);
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var expired = await TokenAsync(id);
        Assert.NotEqual(used, expired);
        await WithDbAsync(db => db.PasswordResetTokens.Where(t => t.TokenHash == ResetTokens.Hash(expired))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        await RefusedAsync(expired);
    }

    [Fact]
    public async Task Asking_again_replaces_the_earlier_link()
    {
        var (id, email) = await PersonAsync();
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var first = await TokenAsync(id);
        await AskedLongAgoAsync(id);   // past the one-a-minute interval (specs/062)
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var second = (await PendingResetEmailsAsync(id)).Select(TokenOf).Single(t => t != first);

        await RefusedAsync(first);
        await SendAsync(new ResetPasswordCommand(second, NewPassword));
    }

    /// <summary>One link, submitted twice at once: one decides, the other is refused like a used link.</summary>
    [Fact]
    public async Task Two_submissions_of_one_link_at_once_reset_once()
    {
        var (id, email) = await PersonAsync();
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var token = await TokenAsync(id);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(async i =>
        {
            try { await SendAsync(new ResetPasswordCommand(token, $"{NewPassword}{i}")); return true; }
            catch (FluentValidation.ValidationException) { return false; }
        }));

        Assert.Equal(1, outcomes.Count(ok => ok));
    }

    [Fact]
    public async Task A_new_password_follows_the_registration_rules()
    {
        var (id, email) = await PersonAsync();
        await SendAsync(new ForgotPasswordCommand(email, "vi"));
        var token = await TokenAsync(id);

        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new ResetPasswordCommand(token, "short")));
        Assert.Contains("at least 8", refused.Message);
    }

    /// <summary>On the record, as Security - and neither the token nor a password is in either entry.</summary>
    [Fact]
    public async Task Both_steps_are_on_the_record_with_no_secret_in_them()
    {
        var (id, email) = await PersonAsync();
        await using var provider = _fixture.For(Guid.Empty);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ForgotPasswordCommand(email, "vi"));
        var token = await TokenAsync(id);
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ResetPasswordCommand(token, NewPassword));

        var entries = harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message)
            .Where(e => e.SubjectId == id.ToString()).ToList();
        Assert.Equal(["PasswordResetRequested", "PasswordReset"], entries.Select(e => e.Action));
        Assert.All(entries, e => Assert.Equal("Security", e.Category));
        var everything = string.Join("|", entries.Select(e => e.Summary + e.Before + e.After));
        Assert.DoesNotContain(token, everything);
        Assert.DoesNotContain(NewPassword, everything);
    }

    // ------------------------------------------------------------------ helpers

    private async Task RefusedAsync(string token)
    {
        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(new ResetPasswordCommand(token, NewPassword)));
        Assert.Contains(ResetTokens.Invalid, refused.Message);
    }

    private async Task<(Guid Id, string Email)> PersonAsync()
    {
        var email = $"reset-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(new RegisterCommand(email, OldPassword, "Lan", "Pham"));
        return (registered.Id, email);
    }

    /// <summary>Moves this person's links two minutes back, past the one-email-a-minute interval (specs/062).</summary>
    private Task AskedLongAgoAsync(Guid id) =>
        WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, DateTime.UtcNow.AddMinutes(-2))));

    private Task<int> CountAsync() => WithDbAsync(db => db.PasswordResetTokens.CountAsync());

    private Task<List<Ecommerce.Domain.Entities.OutgoingEmail>> PendingResetEmailsAsync(Guid id) =>
        WithDbAsync(db => db.OutgoingEmails.AsNoTracking()
            .Where(e => e.RecipientId == id && e.Template == "PasswordReset" && e.Status == Ecommerce.Domain.Entities.OutgoingEmailStatus.Pending)
            .OrderBy(e => e.CreatedAt).ToListAsync());

    /// <summary>The token as the email carries it - what a person would click.</summary>
    private async Task<string> TokenAsync(Guid id) => TokenOf((await PendingResetEmailsAsync(id)).Last());

    private static string TokenOf(Ecommerce.Domain.Entities.OutgoingEmail email) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(email.DataJson)!["token"];

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
