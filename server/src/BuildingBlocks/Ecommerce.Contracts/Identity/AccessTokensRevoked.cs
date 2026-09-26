namespace Ecommerce.Contracts.Identity;

/// <summary>
/// Every access token of <paramref name="UserId"/> issued before <paramref name="RevokedAt"/> stops working
/// (specs/065, #112) - published by Identity when it locks or bans an account, revokes a role, resets or
/// changes a password, or catches a refresh token reused; consumed by every service that validates tokens.
/// </summary>
/// <remarks>
/// It says "tokens issued before now", not "this account is stopped": after a ban the refresh that follows
/// is refused too and the person is signed out; after a password change or a role revoked it succeeds, and
/// the new token carries the new state.
/// </remarks>
/// <param name="Reason">What happened - <c>Locked</c>, <c>Banned</c>, <c>RoleRevoked</c>, <c>PasswordReset</c>,
/// <c>PasswordChanged</c>, <c>SessionReuseDetected</c>. For logs; the rule is the same for all.</param>
public record AccessTokensRevoked(Guid UserId, DateTime RevokedAt, string Reason);
