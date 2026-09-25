namespace Ecommerce.Application.Auth.Common;

/// <summary>
/// What a caller gets back from every path that issues a token.
/// </summary>
/// <param name="Roles">
/// <para>
/// What the caller holds, so a client can decide what to <b>draw</b> (specs/028). A storefront that
/// does not know somebody is a seller cannot offer them a shop, and the alternative - decoding the
/// access token in the browser - was rejected because a decode looks authoritative and the next rule
/// gets written against it.
/// </para>
/// <para>
/// <b>It is not a permission and nothing may treat it as one.</b> Authorization lives in the
/// controller attributes and in the ownership checks; this field only repeats what the server
/// already put in the token. Order is not guaranteed - ask with Contains, never by position.
/// </para>
/// </param>
/// <param name="EmailConfirmed">
/// Whether the address was confirmed by its link (specs/063) - for drawing the "confirm your email" banner,
/// like <paramref name="Roles"/>. The server decides what an unconfirmed account may do on its own.
/// </param>
public record AuthResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Token,
    string RefreshToken,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed
);