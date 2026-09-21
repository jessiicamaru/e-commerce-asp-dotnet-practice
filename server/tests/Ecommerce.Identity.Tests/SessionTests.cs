using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Logout;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Signing out ends the session on the server (feature 015). Without it, a browser that forgets its
/// access token is signed straight back in by the HttpOnly refresh cookie it cannot delete.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class SessionTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    [Fact]
    public async Task After_logout_the_refresh_token_no_longer_opens_a_session()
    {
        var email = $"session-{Guid.NewGuid():N}@example.test";
        await SendAsync(new RegisterCommand(email, "Passw0rd!23", "A", "B"));
        var login = await SendAsync(new LoginCommand(email, "Passw0rd!23"));

        // Works before...
        var refreshed = await SendAsync(new RefreshTokenCommand(login.RefreshToken));

        await SendAsync(new LogoutCommand(refreshed.RefreshToken));

        // ...and not after.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            SendAsync(new RefreshTokenCommand(refreshed.RefreshToken)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Logging_out_without_a_valid_session_is_a_quiet_no_op(string? token)
    {
        await SendAsync(new LogoutCommand(token));   // does not throw
    }
}
