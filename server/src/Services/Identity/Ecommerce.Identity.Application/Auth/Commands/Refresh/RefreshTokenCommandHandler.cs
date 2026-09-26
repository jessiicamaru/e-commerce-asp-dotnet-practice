using Ecommerce.Application.Email;
using Ecommerce.Contracts.Identity;
using MassTransit;
using Ecommerce.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Domain.Entities;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Application.Auth.Commands.Refresh;

/// <summary>
/// Trades a refresh token for a new pair, and notices when a used one comes back (issue #29).
/// </summary>
/// <remarks>
/// <para>
/// <b>Rotation revokes; it does not delete.</b> The old token's row stays, with <c>RevokedAt</c> and
/// <c>ReplacedByToken</c> set. Deleting it made a replayed token look exactly like a typo, so a thief
/// who used a stolen token after its owner had refreshed was indistinguishable from nobody.
/// </para>
/// <para>
/// <b>A revoked token presented again is reuse</b>: two parties hold the same token, and the server
/// cannot tell which is the owner. Every session of that user is revoked, the legitimate one included,
/// which is the trade-off the OAuth 2.0 Security BCP recommends. Losing a session is recoverable;
/// leaving a thief signed in is not.
/// </para>
/// <para>
/// <b>Except within <see cref="ReuseGrace"/> of the rotation.</b> Two tabs share one HttpOnly cookie,
/// so both can send the same token at the same moment. The second is refused with an ordinary 401, and
/// nothing else is revoked. That tab picks up the new cookie on its next request.
/// </para>
/// </remarks>
public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    ILogger<RefreshTokenCommandHandler> logger,
    IAuditTrail audit,
    IPublishEndpoint publishEndpoint
) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(10);

    private const string NotValid = "The session is not valid. Sign in again.";

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<RefreshTokenCommandHandler> _logger = logger;
    private readonly IAuditTrail _audit = audit;

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var user = await _userRepository.GetByUserRefreshTokenAsync(request.RefreshToken, cancellationToken)
            ?? throw new UnauthorizedAccessException(NotValid);

        var presented = user.RefreshTokens.First(t => t.Token == request.RefreshToken);

        if (presented.RevokedAt is { } revokedAt)
        {
            // Reuse is a ROTATED token presented again after the grace window: two parties hold it. A token
            // revoked WITHOUT rotation - by a lock, a ban, or an earlier reuse sweep - is a stale tab, not
            // theft; treating it as reuse ended the session a person signed in with after an unlock (#128).
            var reuse = presented.ReplacedByToken is not null && now - revokedAt > ReuseGrace;

            if (reuse)
            {
                // On the record before the sessions end: a security event worth keeping, not just a log line.
                await _audit.RecordAsync(AuditCategory.Security, "SessionReuseDetected", "User", user.Id.ToString(),
                    $"A replaced session token of {user.Email} was presented again; every session ended",
                    actor: AuditActors.Of(user), cancellationToken: cancellationToken);
                // Whoever holds the stolen token may hold an access token too: it stops within seconds (specs/065).
                await _publishEndpoint.Publish(new AccessTokensRevoked(user.Id, now, "SessionReuseDetected"), cancellationToken);
                await _userRepository.SaveChangesAsync(cancellationToken);

                var revoked = await _userRepository.RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);

                // No token value in the log: it is a credential, even a revoked one.
                _logger.LogWarning(
                    "Refresh token reuse for user {UserId}: a token revoked at {RevokedAt:o} was presented again. Revoked {Count} active session(s).",
                    user.Id, revokedAt, revoked);
            }
            else if (presented.ReplacedByToken is null)
            {
                _logger.LogInformation(
                    "A session of user {UserId} ended at {RevokedAt:o} (not rotated: a lock, a ban or a sign-out elsewhere) was presented again; refused.",
                    user.Id, revokedAt);
            }

            // The same answer either way, so the caller learns nothing about which case this was.
            throw new UnauthorizedAccessException(NotValid);
        }

        // Locked or banned (specs/043): their sessions were revoked when it happened, and this holds
        // even for one that slipped through - the account row decides, not the token.
        if (presented.IsExpired || user.IsBanned || user.IsLocked(now))
        {
            throw new UnauthorizedAccessException(NotValid);
        }

        var replacement = new RefreshToken
        {
            Token = _jwtTokenGenerator.GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = now.AddDays(JwtConstants.TokenDurationDay)
        };

        if (!await _userRepository.TryRotateRefreshTokenAsync(request.RefreshToken, replacement, now, cancellationToken))
        {
            // Another request rotated this token between our read and our write: a concurrent
            // refresh, handled as the grace case above.
            throw new UnauthorizedAccessException(NotValid);
        }

        if (EmailTemplates.Supported(request.Language) is { } language && language != user.Language)
        {
            await _userRepository.RecordLanguageAsync(user.Id, language, cancellationToken);
        }

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            _jwtTokenGenerator.GenerateAccessToken(user),
            replacement.Token,
            user.Roles.Select(role => role.Name).ToList(),
            user.EmailConfirmed
        );
    }
}
