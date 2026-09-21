using Ecommerce.Application.Common.Interfaces;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.Logout;

/// <remarks>
/// <para>
/// Signing out has to happen on the server. The refresh token lives in an HttpOnly cookie that no
/// script can read or delete, so a client that only forgets its access token is signed straight back
/// in by the next silent refresh. This deletes the refresh token, so the cookie - even if a copy
/// survives somewhere - no longer opens a session.
/// </para>
/// <para>
/// Idempotent and quiet: no cookie, an unknown token, or one already removed all end the same way.
/// Signing out must never fail in a way the person has to deal with.
/// </para>
/// </remarks>
public class LogoutCommandHandler(IUserRepository users) : IRequestHandler<LogoutCommand>
{
    private readonly IUserRepository _users = users;

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var user = await _users.GetByUserRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var token = user?.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);

        if (user is null || token is null)
        {
            return;
        }

        user.RefreshTokens.Remove(token);
        await _users.SaveChangesAsync(cancellationToken);
    }
}
