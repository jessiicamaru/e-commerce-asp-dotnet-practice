namespace Ecommerce.Shared.Authentication;

/// <summary>
/// Exposes the authenticated caller to the Application layer without leaking HttpContext into it.
/// Identity comes from the validated access token, never from the request body.
/// </summary>
public interface ICurrentUser
{
    Guid? Id { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// Whether the caller holds a role (specs/027). Read from the token, like everything else here.
    /// </summary>
    /// <remarks>
    /// <c>[Authorize(Roles = ...)]</c> covers the checks a controller can make before anything is
    /// read. This exists for the ones it cannot: "may this seller write to THIS product" depends on
    /// a row, and an attribute runs too early to have seen one.
    /// </remarks>
    bool IsInRole(string role);
}
