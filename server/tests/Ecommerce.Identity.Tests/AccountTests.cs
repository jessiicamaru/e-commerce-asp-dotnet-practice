using Ecommerce.Application.Auth.Commands.Account;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// A person changes their own name, phone and password (specs/064, #104) - the account from the token, the
/// password only with the current one, and every other session ended by the change.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class AccountTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private const string NewPassword = "N3w-Passw0rd!";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task I_read_and_change_my_own_details()
    {
        var (id, email) = await PersonAsync();

        var me = await SendAsync(id, new GetMeQuery());
        Assert.Equal((email, "Lan", "Pham", (string?)null), (me.Email, me.FirstName, me.LastName, me.Phone));

        var changed = await SendAsync(id, new UpdateMeCommand("  Mai ", "Tran", " 0912 345 678 "));
        Assert.Equal(("Mai", "Tran", "0912 345 678"), (changed.FirstName, changed.LastName, changed.Phone));
        Assert.Equal(changed, await SendAsync(id, new GetMeQuery()));

        // The name reaches the next session - and the given_name a review is signed with.
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        Assert.Equal("Mai", session.FirstName);
    }

    [Fact]
    public async Task An_empty_name_is_refused_and_nothing_changes()
    {
        var (id, _) = await PersonAsync();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => SendAsync(id, new UpdateMeCommand(" ", "Tran", null)));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => SendAsync(id, new UpdateMeCommand("Mai", "Tran", new string('9', 21))));

        Assert.Equal("Lan", (await SendAsync(id, new GetMeQuery())).FirstName);
    }

    [Fact]
    public async Task A_change_of_details_is_on_the_record_with_before_and_after()
    {
        var (id, _) = await PersonAsync();
        var entries = await RecordedAsync(id, new UpdateMeCommand("Mai", "Tran", "0912"));

        var entry = Assert.Single(entries);
        Assert.Equal(("User", "ProfileUpdated", id.ToString()), (entry.Category, entry.Action, entry.SubjectId));
        Assert.Contains("Lan", entry.Before);
        Assert.Contains("Mai", entry.After);
    }

    // ------------------------------------------------------------------ the password

    [Fact]
    public async Task A_new_password_keeps_this_session_and_ends_every_other()
    {
        var (id, email) = await PersonAsync();
        var here = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        var elsewhere = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        await SendAsync(id, new ChangePasswordCommand(Password, NewPassword) { KeepRefreshToken = here.RefreshToken });

        await SendAsync(Guid.Empty, new RefreshTokenCommand(here.RefreshToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new RefreshTokenCommand(elsewhere.RefreshToken)));
        await SendAsync(Guid.Empty, new LoginCommand(email, NewPassword));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));
    }

    [Fact]
    public async Task Without_a_session_to_keep_every_session_ends()
    {
        var (id, email) = await PersonAsync();
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        await SendAsync(id, new ChangePasswordCommand(Password, NewPassword));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken)));
    }

    [Fact]
    public async Task A_wrong_current_password_is_refused_and_nothing_changes()
    {
        var (id, email) = await PersonAsync();
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));

        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(id, new ChangePasswordCommand("not-it", NewPassword)));
        Assert.Contains(AccountHandlers.WrongPassword, refused.Message);

        await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        await SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken));
    }

    [Fact]
    public async Task A_new_password_follows_the_registration_rules()
    {
        var (id, _) = await PersonAsync();

        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(id, new ChangePasswordCommand(Password, "short")));
        Assert.Contains("at least 8", refused.Message);
    }

    /// <summary>A stolen access token must not guess the password faster than the sign-in page allows (specs/062).</summary>
    [Fact]
    public async Task Wrong_current_passwords_count_toward_the_sign_in_pause()
    {
        var (id, email) = await PersonAsync();

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => SendAsync(id, new ChangePasswordCommand("guess", NewPassword)));
        }

        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(id, new ChangePasswordCommand(Password, NewPassword)));
        await Assert.ThrowsAsync<TooManyRequestsException>(() => SendAsync(Guid.Empty, new LoginCommand(email, Password)));
    }

    [Fact]
    public async Task A_change_of_password_is_on_the_record_without_it()
    {
        var (id, _) = await PersonAsync();
        var entries = await RecordedAsync(id, new ChangePasswordCommand(Password, NewPassword));

        var entry = Assert.Single(entries);
        Assert.Equal(("Security", "PasswordChanged"), (entry.Category, entry.Action));
        Assert.DoesNotContain(NewPassword, entry.Summary + entry.Before + entry.After);
        Assert.DoesNotContain(Password, entry.Summary + entry.Before + entry.After);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(Guid Id, string Email)> PersonAsync()
    {
        var email = $"account-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Lan", "Pham"));
        return (registered.Id, email);
    }

    private async Task<List<AuditEntryRecorded>> RecordedAsync(Guid caller, IBaseRequest request)
    {
        await using var provider = _fixture.For(caller);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send((object)request);
        return harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).ToList();
    }

    private async Task SendAsync(Guid caller, IRequest request)
    {
        await using var provider = _fixture.For(caller);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request)
    {
        await using var provider = _fixture.For(caller);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
