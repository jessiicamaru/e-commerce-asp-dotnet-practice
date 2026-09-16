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
}
