using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Ordinary client mistakes are client errors (issue #28). Each of these threw a bare
/// <see cref="Exception"/>, which the shared handler cannot map, so they reached the client as 500.
/// </summary>
/// <remarks>
/// The assertion is the exception TYPE, because that is what decides the status code: the shared
/// handler maps <see cref="ConflictException"/> to 409 and <see cref="UnauthorizedAccessException"/>
/// to 401, and anything else to 500.
/// </remarks>
[Collection(nameof(IdentityTestCollection))]
public class AuthErrorTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static string NewEmail() => $"auth-{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task Registering_an_email_twice_is_a_conflict_not_a_server_error()
    {
        var email = NewEmail();
        await SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B")));
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_are_the_same_401()
    {
        var email = NewEmail();
        await SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B"));

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(new LoginCommand(email, "not-the-password")));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(new LoginCommand(NewEmail(), "whatever")));

        // Identical, so the answer cannot be used to learn which emails have accounts.
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Fact]
    public async Task A_bogus_refresh_token_is_401()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(new RefreshTokenCommand("not-a-real-token")));
    }

    [Fact]
    public async Task The_right_password_still_signs_in()
    {
        var email = NewEmail();
        await SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B"));

        var result = await SendAsync(new LoginCommand(email, "Passw0rd!23"));

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }
}
