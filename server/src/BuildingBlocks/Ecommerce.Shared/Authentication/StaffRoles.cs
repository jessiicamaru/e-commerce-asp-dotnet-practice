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
}
