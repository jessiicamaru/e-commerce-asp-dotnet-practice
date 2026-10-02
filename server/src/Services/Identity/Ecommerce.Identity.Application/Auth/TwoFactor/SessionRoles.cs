using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Auth.TwoFactor;

/// <summary>
/// Which of a person's roles a session carries: staff roles only when the session was verified with a second factor
/// (#218, specs/110 research D2) AND was made for the back office (#278, specs/138, ADR-003). Every service authorizes
/// from the token's roles, so withholding them here is what makes every staff endpoint in every service refuse an
/// unverified session, and any storefront session - with no change anywhere else.
/// </summary>
/// <remarks>
/// ⚠️ The ONE place this is decided. The access token and <c>AuthResponse.Roles</c> both come from <see cref="Of"/>, so
/// what the storefront draws is what the token grants.
/// </remarks>
public static class SessionRoles
{
    /// <summary>The roles that need a second factor: whoever holds one can act on other people.</summary>
    public static readonly IReadOnlyCollection<string> Staff = [RoleNames.Admin, RoleNames.Moderator];

    public const string Required = "Required";
    public const string SetupRequired = "SetupRequired";

    public static bool IsStaff(User user) => user.Roles.Any(role => Staff.Contains(role.Name));

    public static IReadOnlyList<string> Of(User user, bool twoFactorVerified, SessionClient client) =>
        user.Roles.Select(role => role.Name)
            .Where(name => !Staff.Contains(name) || (twoFactorVerified && client == SessionClient.BackOffice))
            .ToList();

    /// <summary>What the storefront is told about a session it holds: staff without 2FA must set it up.</summary>
    public static string? State(User user) => IsStaff(user) && !user.TwoFactorEnabled ? SetupRequired : null;
}
