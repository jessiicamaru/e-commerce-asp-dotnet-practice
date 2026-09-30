using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    /// <summary>
    /// A signed access token. Staff roles are written only when <paramref name="twoFactorVerified"/> (#218, specs/110):
    /// the session was established with a second factor.
    /// </summary>
    string GenerateAccessToken(User user, bool twoFactorVerified = false);

    string GenerateRefreshToken();
}