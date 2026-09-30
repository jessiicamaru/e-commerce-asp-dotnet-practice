using System.Security.Cryptography;
using System.Text;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Auth.SignInThrottling;
using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Constants;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Email;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Application.Auth.TwoFactor;

// ---------------------------------------------------------------------------------------------- requests and answers

/// <summary>The second step of signing in (#218, specs/110): the challenge from the first, and a code or a recovery code.</summary>
public record LoginTwoFactorCommand(string Challenge, string? Code, string? RecoveryCode) : IRequest<AuthResponse>;

/// <summary>The caller's own two-factor state.</summary>
public record GetMyTwoFactorQuery : IRequest<TwoFactorStatus>;

/// <param name="Required">The caller is staff, who must keep it on.</param>
public record TwoFactorStatus(bool Enabled, DateTime? EnabledAt, int RecoveryCodesLeft, bool Required);

/// <summary>Starts setting up: a new secret, returned ONCE - as base32 for typing and as the URI the QR code draws.</summary>
public record StartTwoFactorSetupCommand : IRequest<TwoFactorSetup>;

public record TwoFactorSetup(string Secret, string Uri);

/// <summary>
/// Confirms setup with a code from the app, proving it saved the right secret. <paramref name="KeepRefreshToken"/> is
/// the session doing it - from the HttpOnly cookie, never the body - which becomes verified; every other session ends.
/// </summary>
public record ConfirmTwoFactorCommand(string Code, string? KeepRefreshToken = null) : IRequest<RecoveryCodesResponse>;

/// <summary>A fresh set of recovery codes, which needs a current code; the old set stops working.</summary>
public record NewRecoveryCodesCommand(string Code) : IRequest<RecoveryCodesResponse>;

/// <summary>The ten recovery codes, shown once - named as the contract says: <c>recoveryCodes</c>.</summary>
public record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>Turns it off - the owner, with their password and a code. Refused to staff, who must keep it.</summary>
public record DisableTwoFactorCommand(string Password, string Code) : IRequest;

/// <summary>An administrator resets another person's second factor - a lost phone and no recovery codes left.</summary>
public record ResetTwoFactorCommand(Guid UserId) : IRequest;

public class LoginTwoFactorCommandValidator : AbstractValidator<LoginTwoFactorCommand>
{
    public LoginTwoFactorCommandValidator()
    {
        RuleFor(x => x.Challenge).NotEmpty().WithMessage("Sign in again.");
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.Code) != string.IsNullOrWhiteSpace(x.RecoveryCode))
            .WithName("Code").WithMessage("Give the code from your app, or one recovery code.");
    }
}

public class ConfirmTwoFactorCommandValidator : AbstractValidator<ConfirmTwoFactorCommand>
{
    public ConfirmTwoFactorCommandValidator() => RuleFor(x => x.Code).NotEmpty();
}

public class NewRecoveryCodesCommandValidator : AbstractValidator<NewRecoveryCodesCommand>
{
    public NewRecoveryCodesCommandValidator() => RuleFor(x => x.Code).NotEmpty();
}

public class DisableTwoFactorCommandValidator : AbstractValidator<DisableTwoFactorCommand>
{
    public DisableTwoFactorCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}

// ---------------------------------------------------------------------------------------------- the pieces

/// <summary>Random tokens and codes, and the hashes they are kept as.</summary>
public static class TwoFactorCodes
{
    public const int RecoveryCodeCount = 10;

    public const string WrongCode = "The code is not right, or was already used.";
    public const string SignInAgain = "Sign in again.";

    /// <summary>Hex SHA-256 - how a challenge and a recovery code are stored and looked up.</summary>
    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string NewChallenge() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Ten codes of ten base32 characters, shown as <c>XXXXX-XXXXX</c>: 50 bits each, typed once, if ever.</summary>
    public static List<string> NewRecoveryCodes() =>
        Enumerable.Range(0, RecoveryCodeCount).Select(_ =>
        {
            var code = Base32.Encode(RandomNumberGenerator.GetBytes(7))[..10];
            return code[..5] + "-" + code[5..];
        }).ToList();

    /// <summary>A recovery code as it is hashed: upper case, no dash or spaces - however it was typed.</summary>
    public static string NormaliseRecoveryCode(string code) =>
        code.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static ValidationException Invalid(string property, string message) => new([new ValidationFailure(property, message)]);
}

// ---------------------------------------------------------------------------------------------- the handlers

public class TwoFactorHandlers(
    IUserRepository users,
    ITwoFactorRepository twoFactor,
    ITwoFactorSecretProtector protector,
    IJwtTokenGenerator tokens,
    IPasswordHasher passwords,
    ISignInThrottle throttle,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IEmailSender email,
    IPublishEndpoint publish,
    IOptions<TwoFactorOptions> settings) :
    IRequestHandler<LoginTwoFactorCommand, AuthResponse>,
    IRequestHandler<GetMyTwoFactorQuery, TwoFactorStatus>,
    IRequestHandler<StartTwoFactorSetupCommand, TwoFactorSetup>,
    IRequestHandler<ConfirmTwoFactorCommand, RecoveryCodesResponse>,
    IRequestHandler<NewRecoveryCodesCommand, RecoveryCodesResponse>,
    IRequestHandler<DisableTwoFactorCommand>,
    IRequestHandler<ResetTwoFactorCommand>
{
    private readonly IUserRepository _users = users;
    private readonly ITwoFactorRepository _twoFactor = twoFactor;
    private readonly ITwoFactorSecretProtector _protector = protector;
    private readonly IJwtTokenGenerator _tokens = tokens;
    private readonly IPasswordHasher _passwords = passwords;
    private readonly ISignInThrottle _throttle = throttle;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly IEmailSender _email = email;
    private readonly IPublishEndpoint _publish = publish;
    private readonly TwoFactorOptions _settings = settings.Value;

    // ------------------------------------------------------------------ signing in

    public async Task<AuthResponse> Handle(LoginTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var challengeHash = TwoFactorCodes.Hash(request.Challenge);
        var challenge = await _twoFactor.GetLiveChallengeAsync(challengeHash, now, cancellationToken)
            ?? throw TwoFactorCodes.Invalid(nameof(request.Challenge), TwoFactorCodes.SignInAgain);

        var user = await _users.GetByIdAsync(challenge.UserId, cancellationToken)
            ?? throw TwoFactorCodes.Invalid(nameof(request.Challenge), TwoFactorCodes.SignInAgain);

        // The pause of specs/062 covers codes too (research D7): asked before the code, so a paused account's right
        // code waits like its right password does.
        var emailKey = EmailKey.For(user.Email);
        if (await _throttle.BlockedUntilAsync(emailKey, now, cancellationToken) is { } pausedUntil)
            throw new TooManyRequestsException("Too many wrong attempts for this email. Try again later.", pausedUntil - now);

        // Stopped by staff while the code was being typed: the same refusal as the first step's.
        if (user.IsBanned || user.IsLocked(now) || !user.TwoFactorEnabled)
            throw TwoFactorCodes.Invalid(nameof(request.Challenge), TwoFactorCodes.SignInAgain);

        var byRecoveryCode = !string.IsNullOrWhiteSpace(request.RecoveryCode);
        var accepted = byRecoveryCode
            ? await _twoFactor.TryUseRecoveryCodeAsync(
                user.Id, TwoFactorCodes.Hash(TwoFactorCodes.NormaliseRecoveryCode(request.RecoveryCode!)), now, cancellationToken)
            : await AcceptCodeAsync(user, request.Code, now, cancellationToken);

        if (!accepted)
        {
            await _twoFactor.RecordChallengeFailureAsync(challengeHash, cancellationToken);
            await _throttle.RecordFailureAsync(emailKey, now, cancellationToken);
            await _audit.RecordAsync(AuditCategory.Security, "SignInRefused", "User", user.Id.ToString(),
                $"Sign-in refused for {user.Email}: a wrong {(byRecoveryCode ? "recovery code" : "two-factor code")}",
                actor: AuditActors.Of(user), cancellationToken: cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
            throw TwoFactorCodes.Invalid(byRecoveryCode ? nameof(request.RecoveryCode) : nameof(request.Code), TwoFactorCodes.WrongCode);
        }

        // Claimed once: a second exchange of the same challenge - even with another right code - is refused.
        if (!await _twoFactor.TryUseChallengeAsync(challengeHash, now, cancellationToken))
            throw TwoFactorCodes.Invalid(nameof(request.Challenge), TwoFactorCodes.SignInAgain);

        // Only now, after the code: clearing at the password would let somebody who has it restart the count forever.
        await _throttle.ClearAsync(emailKey, cancellationToken);

        var refreshToken = new RefreshToken
        {
            Token = _tokens.GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = now.AddDays(JwtConstants.TokenDurationDay),
            TwoFactorVerified = true,
        };
        user.RefreshTokens.Add(refreshToken);

        if (byRecoveryCode)
        {
            await _audit.RecordAsync(AuditCategory.Security, "TwoFactorRecoveryCodeUsed", "User", user.Id.ToString(),
                $"{user.Email} signed in with a recovery code", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        }

        await _audit.RecordAsync(AuditCategory.Security, "SignedIn", "User", user.Id.ToString(),
            $"{user.Email} signed in with a second factor", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            user.Id, user.Email, user.FirstName, user.LastName,
            _tokens.GenerateAccessToken(user, twoFactorVerified: true),
            refreshToken.Token,
            SessionRoles.Of(user, twoFactorVerified: true),
            user.EmailConfirmed);
    }

    // ------------------------------------------------------------------ the caller's own

    public async Task<TwoFactorStatus> Handle(GetMyTwoFactorQuery request, CancellationToken cancellationToken)
    {
        var user = await MeAsync(cancellationToken);
        var left = user.TwoFactorEnabled ? await _twoFactor.CountRecoveryCodesLeftAsync(user.Id, cancellationToken) : 0;
        return new TwoFactorStatus(user.TwoFactorEnabled, user.TwoFactorEnabledAt, left, SessionRoles.IsStaff(user));
    }

    public async Task<TwoFactorSetup> Handle(StartTwoFactorSetupCommand request, CancellationToken cancellationToken)
    {
        var user = await MeAsync(cancellationToken);
        if (user.TwoFactorEnabled)
            throw new ConflictException("Two-factor sign-in is already on.");

        // A new secret each time: an abandoned setup's QR code, photographed or left on a screen, is worth nothing.
        var secret = Totp.NewSecret();
        user.TwoFactorSecret = _protector.Protect(secret);
        user.TwoFactorLastStep = null;
        await _users.SaveChangesAsync(cancellationToken);

        return new TwoFactorSetup(Base32.Encode(secret), Totp.KeyUri(_settings.Issuer, user.Email, secret));
    }

    public async Task<RecoveryCodesResponse> Handle(ConfirmTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await MeAsync(cancellationToken);
        if (user.TwoFactorEnabled || user.TwoFactorSecret is null)
            throw new ConflictException(user.TwoFactorEnabled ? "Two-factor sign-in is already on." : "Start setting it up first.");

        var now = DateTime.UtcNow;
        if (!await AcceptCodeAsync(user, request.Code, now, cancellationToken))
            throw TwoFactorCodes.Invalid(nameof(request.Code), TwoFactorCodes.WrongCode);

        var codes = TwoFactorCodes.NewRecoveryCodes();
        await _twoFactor.ReplaceRecoveryCodesAsync(user.Id, codes.Select(c => TwoFactorCodes.Hash(TwoFactorCodes.NormaliseRecoveryCode(c))).ToList(), now, cancellationToken);
        user.TwoFactorEnabledAt = now;

        await _audit.RecordAsync(AuditCategory.Security, "TwoFactorEnabled", "User", user.Id.ToString(),
            $"{user.Email} turned on two-factor sign-in", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        // This session proved the second factor by confirming it; every other one never did, and ends.
        await _twoFactor.VerifySessionAndEndOthersAsync(user.Id, request.KeepRefreshToken, now, cancellationToken);
        return new RecoveryCodesResponse(codes);
    }

    public async Task<RecoveryCodesResponse> Handle(NewRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        var user = await MeAsync(cancellationToken);
        if (!user.TwoFactorEnabled)
            throw new ConflictException("Two-factor sign-in is off.");

        var now = DateTime.UtcNow;
        if (!await AcceptCodeAsync(user, request.Code, now, cancellationToken))
            throw TwoFactorCodes.Invalid(nameof(request.Code), TwoFactorCodes.WrongCode);

        var codes = TwoFactorCodes.NewRecoveryCodes();
        await _twoFactor.ReplaceRecoveryCodesAsync(user.Id, codes.Select(c => TwoFactorCodes.Hash(TwoFactorCodes.NormaliseRecoveryCode(c))).ToList(), now, cancellationToken);
        await _audit.RecordAsync(AuditCategory.Security, "TwoFactorRecoveryCodesRenewed", "User", user.Id.ToString(),
            $"{user.Email} made new recovery codes", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesResponse(codes);
    }

    public async Task Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await MeAsync(cancellationToken);
        if (SessionRoles.IsStaff(user))
            throw new ForbiddenException("Staff accounts keep two-factor sign-in on.");
        if (!user.TwoFactorEnabled)
            throw new ConflictException("Two-factor sign-in is off.");

        var now = DateTime.UtcNow;
        // A wrong password counts toward the sign-in pause, as on every page that asks for it (specs/064).
        if (!_passwords.VerifyPassword(user.PasswordHash, request.Password))
        {
            await _throttle.RecordFailureAsync(EmailKey.For(user.Email), now, cancellationToken);
            throw TwoFactorCodes.Invalid(nameof(request.Password), "The password is not right.");
        }

        if (!await AcceptCodeAsync(user, request.Code, now, cancellationToken))
            throw TwoFactorCodes.Invalid(nameof(request.Code), TwoFactorCodes.WrongCode);

        TurnOff(user);
        await _twoFactor.DeleteAllAsync(user.Id, cancellationToken);
        await _audit.RecordAsync(AuditCategory.Security, "TwoFactorDisabled", "User", user.Id.ToString(),
            $"{user.Email} turned off two-factor sign-in", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ staff

    public async Task Handle(ResetTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var admin = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        if (!_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenException("Only an administrator resets two-factor sign-in.");
        // The recovery path is how 2FA is got round: nobody takes it for themselves (research D8).
        if (request.UserId == admin)
            throw new ForbiddenException("Nobody resets their own two-factor sign-in. Ask another administrator.");

        var user = await _users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");
        if (!user.TwoFactorEnabled && user.TwoFactorSecret is null)
            throw new ConflictException("Two-factor sign-in is off for this person.");

        var now = DateTime.UtcNow;
        TurnOff(user);
        await _twoFactor.DeleteAllAsync(user.Id, cancellationToken);

        // Their sessions end, and an access token already out stops within seconds (specs/065).
        await _publish.Publish(new AccessTokensRevoked(user.Id, now, "TwoFactorReset"), cancellationToken);
        await _audit.RecordAsync(AuditCategory.Security, "TwoFactorReset", "User", user.Id.ToString(),
            $"Two-factor sign-in of {user.Email} was reset by an administrator", cancellationToken: cancellationToken, aboutUserId: user.Id);
        // Told at once and in their words: a reset they did not ask for is how an account is taken.
        await _email.SendAsync(user.Id, EmailTemplate.TwoFactorReset,
            new Dictionary<string, string>(), EmailTemplate.ReadersLanguage, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
        await _users.RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
    }

    // ------------------------------------------------------------------ shared

    /// <summary>
    /// The code against the stored secret, then the window recorded by one guarded statement (research D5) - of two
    /// requests with the same code, one gets true.
    /// </summary>
    private async Task<bool> AcceptCodeAsync(User user, string? code, DateTime now, CancellationToken cancellationToken)
    {
        if (user.TwoFactorSecret is null)
            return false;

        var step = Totp.Match(_protector.Unprotect(user.TwoFactorSecret), code, now, user.TwoFactorLastStep);
        return step is { } matched && await _twoFactor.TryAcceptStepAsync(user.Id, matched, cancellationToken);
    }

    private static void TurnOff(User user)
    {
        user.TwoFactorSecret = null;
        user.TwoFactorEnabledAt = null;
        user.TwoFactorLastStep = null;
    }

    private async Task<User> MeAsync(CancellationToken cancellationToken)
    {
        var id = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return await _users.GetByIdAsync(id, cancellationToken) ?? throw new UnauthorizedAccessException("No such account.");
    }
}
