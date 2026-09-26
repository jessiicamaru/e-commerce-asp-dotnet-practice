using Ecommerce.Application.Email;
using Ecommerce.Application.Common;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Auth.SignInThrottling;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Domain.Entities;
using MediatR;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Application.Auth.Commands.Login;

public class LoginCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator,
    IAuditTrail audit, ISignInThrottle throttle, IOptions<SignInOptions> signInOptions) : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IAuditTrail _audit = audit;
    private readonly ISignInThrottle _throttle = throttle;
    private readonly SignInOptions _signIn = signInOptions.Value;

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Paused after too many wrong passwords for this email (specs/062) - asked BEFORE the account, so an
        // address with no account is paused exactly like one with an account (#28), and before the password,
        // so the right one waits too: otherwise the pause would still answer "right" or "wrong" to a guesser.
        var emailKey = EmailKey.For(request.Email);
        var now = DateTime.UtcNow;
        if (await _throttle.BlockedUntilAsync(emailKey, now, cancellationToken) is { } pausedUntil)
        {
            throw new TooManyRequestsException(
                "Too many wrong passwords for this email. Try again later.", pausedUntil - now);
        }

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            // Counted per email in one statement (specs/062). The attempt that starts the pause still gets
            // the ordinary 401; the ones after it get 429.
            var failure = await _throttle.RecordFailureAsync(emailKey, now, cancellationToken);
            if (failure.Paused && user is not null)
            {
                await _audit.RecordAsync(
                    AuditCategory.Security, "SignInThrottled", "User", user.Id.ToString(),
                    $"Sign-in for {user.Email} paused for {_signIn.CooldownMinutes} minutes after {_signIn.MaxFailures} wrong passwords",
                    actor: new AuditActor(null, null, null), cancellationToken: cancellationToken);
            }

            // 401, and the same message for "no such email" and "wrong password" so the answer cannot be
            // used to learn which emails have accounts. Was a bare Exception - a 500 (issue #28).
            // Refused sign-ins are the Security log's reason to exist (specs/041). Saved on their own - the
            // outbox row is the only write - because the refusal below leaves nothing else to save.
            await _audit.RecordAsync(
                AuditCategory.Security, "SignInRefused", "User", user?.Id.ToString(),
                $"Sign-in refused for {request.Email.Trim()}",
                // No such account: nobody - never "whoever is calling", which on this anonymous endpoint means nothing.
                actor: user is null ? new AuditActor(null, null, null) : AuditActors.Of(user),
                cancellationToken: cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // The right password: the count of wrong ones starts again (specs/062).
        await _throttle.ClearAsync(emailKey, cancellationToken);

        // Stopped by staff (specs/043). Only now, after the right password: before it, a locked account
        // and a wrong password must look the same (#28). After it, the person is who they say they are
        // and is owed the reason.
        if (user.IsBanned || user.IsLocked(now))
        {
            var why = user.IsBanned
                ? $"This account is banned: {user.BanReason}"
                : $"This account is locked until {user.LockedUntil:yyyy-MM-dd HH:mm} UTC: {user.LockReason}";

            await _audit.RecordAsync(
                AuditCategory.Security, "SignInRefused", "User", user.Id.ToString(),
                $"Sign-in refused for {user.Email}: {(user.IsBanned ? "banned" : "locked")}",
                actor: AuditActors.Of(user), cancellationToken: cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            // The facts beside the sentence, so the storefront says it in the reader's language and time
            // zone (specs/049); the sentence stays for anything that only reads `detail`.
            throw new ForbiddenException(why, user.IsBanned
                ? new Dictionary<string, object?> { ["code"] = "AccountBanned", ["reason"] = user.BanReason }
                : new Dictionary<string, object?>
                {
                    ["code"] = "AccountLocked",
                    ["until"] = DateTime.SpecifyKind(user.LockedUntil!.Value, DateTimeKind.Utc),
                    ["reason"] = user.LockReason,
                });
        }

        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshTokenString = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenString,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.TokenDurationDay)
        });

        // The language they are using the shop in, for the emails that cannot know it (specs/083).
        if (EmailTemplates.Supported(request.Language) is { } language)
        {
            user.Language = language;
        }

        await _audit.RecordAsync(
            AuditCategory.Security, "SignedIn", "User", user.Id.ToString(), $"{user.Email} signed in",
            actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshTokenString,
            user.Roles.Select(role => role.Name).ToList(),
            user.EmailConfirmed
        );
    }
}