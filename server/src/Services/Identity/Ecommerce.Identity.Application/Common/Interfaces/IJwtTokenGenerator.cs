using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    /// <summary>
    /// A signed access token. Staff roles are written only when <paramref name="twoFactorVerified"/> (#218, specs/110) and
    /// the session is the back office's (#278, specs/138) - <see cref="Auth.TwoFactor.SessionRoles.Of"/> decides.
    /// </summary>
    string GenerateAccessToken(User user, bool twoFactorVerified, SessionClient client);

    string GenerateRefreshToken();
}