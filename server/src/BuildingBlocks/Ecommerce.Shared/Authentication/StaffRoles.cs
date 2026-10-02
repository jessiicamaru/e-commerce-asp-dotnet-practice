using System.Security.Claims;

namespace Ecommerce.Shared.Authentication;

/// <summary>
/// The people who run the shop (specs/043), as one list every service can name:
/// <c>[Authorize(Roles = StaffRoles.Staff)]</c>.
/// </summary>
/// <remarks>
/// An attribute decides who reaches an endpoint. What a moderator may do to WHOM - never to an
/// administrator, never to themselves - depends on the target's row and is checked in the handler.
/// </remarks>
public static class StaffRoles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";

    /// <summary>Admin or Moderator - the comma is ASP.NET Core's "any of".</summary>
    public const string Staff = Admin + "," + Moderator;

    /// <summary>
    /// Removes Admin and Moderator from a principal whose token was not issued for the back office (#280, specs/139) -
    /// the second line behind Identity's own rule (specs/138), held by every service that validates a token. Every
    /// other role, and the token itself, are left alone: it can still do what it was issued for.
    /// </summary>
    public static void KeepOnlyInTheBackOffice(ClaimsPrincipal principal, string backOfficeAudience)
    {
        if (principal.FindAll("aud").Any(aud => aud.Value == backOfficeAudience))
        {
            return;
        }

        foreach (var identity in principal.Identities)
        {
            foreach (var role in identity.FindAll(identity.RoleClaimType).Where(c => c.Value is Admin or Moderator).ToList())
            {
                identity.RemoveClaim(role);
            }
        }
    }
}
