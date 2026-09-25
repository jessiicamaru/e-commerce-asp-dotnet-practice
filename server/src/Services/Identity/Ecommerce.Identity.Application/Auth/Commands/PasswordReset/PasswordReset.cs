using System.Security.Cryptography;
using System.Text;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.SignInThrottling;
using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Application.Email;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Email;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.PasswordReset;

/// <summary>
/// "I forgot my password" (specs/061). Always completes the same way - whether or not the address has an
/// account - so the answer reveals nothing about which emails exist (#28).
/// </summary>
/// <param name="Language">What the email is written in; the controller reads it from <c>Accept-Language</c>.</param>
public record ForgotPasswordCommand(string Email, string Language = "") : IRequest;

/// <summary>Chooses a new password with the token from the link (specs/061).</summary>
public record ResetPasswordCommand(string Token, string Password) : IRequest;

/// <summary>The links a person asked for, kept as hashes (specs/061).</summary>
public interface IPasswordResetRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a link was asked for this person since <paramref name="since"/>. Locks the person's row first,
    /// so two requests at once are decided one after the other (specs/062). Call inside the transaction.
    /// </summary>
    Task<bool> AskedSinceAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>Removes a person's links that were never used: the newest one they asked for is the only way in.</summary>
    Task DeleteUnusedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses a link in ONE guarded statement - unused and unexpired, or nothing - and returns whose it was.
    /// Two submissions of one link at once: one gets the user, the other gets null.
    /// </summary>
    Task<Guid?> TryClaimAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);
}

public static class ResetTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <summary>At most one email per address in this long (specs/062), however many IPs ask.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    public const string Invalid = "This link is invalid or has expired. Ask for a new one.";

    /// <summary>32 random bytes, base64url - what goes in the link.</summary>
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>What is stored: the hex SHA-256 of the token. The token itself is never stored.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() =>
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.").MaximumLength(255);
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage(ResetTokens.Invalid).MaximumLength(200).WithMessage(ResetTokens.Invalid);

        // The same rules as registration: a reset must not be the way round them.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(RegisterCommandValidator.MinimumPasswordLength)
            .WithMessage($"Password must be at least {RegisterCommandValidator.MinimumPasswordLength} characters.")
            .Must(p => p is null || Encoding.UTF8.GetByteCount(p) <= RegisterCommandValidator.MaximumPasswordBytes)
            .WithMessage($"Password must be at most {RegisterCommandValidator.MaximumPasswordBytes} bytes.");
    }
}

public class PasswordResetHandlers(
    IUserRepository users,
    IPasswordResetRepository resets,
    IOutgoingEmailRepository emails,
    IPasswordHasher hasher,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ISignInThrottle throttle) :
    IRequestHandler<ForgotPasswordCommand>,
    IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _users = users;
    private readonly IPasswordResetRepository _resets = resets;
    private readonly IOutgoingEmailRepository _emails = emails;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAuditTrail _audit = audit;
    private readonly ISignInThrottle _throttle = throttle;

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // The same completion as for a real account: nothing here tells a stranger which emails exist.
            return;
        }

        var now = DateTime.UtcNow;
        var token = ResetTokens.NewToken();

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // One email a minute per address (specs/062): a stranger asking from many IPs cannot fill an
            // inbox. Still the same 202 - the answer must not differ from any other request.
            if (await _resets.AskedSinceAsync(user.Id, now - ResetTokens.MinimumInterval, ct))
            {
                return;
            }

            await _resets.DeleteUnusedAsync(user.Id, ct);
            await _resets.AddAsync(new PasswordResetToken
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                TokenHash = ResetTokens.Hash(token),
                ExpiresAt = now + ResetTokens.Lifetime,
                CreatedAt = now,
            }, ct);

            await _audit.RecordAsync(AuditCategory.Security, "PasswordResetRequested", "User", user.Id.ToString(),
                $"A password reset link was sent to {user.Email}", actor: AuditActors.Of(user), cancellationToken: ct);
            await _users.SaveChangesAsync(ct);

            // Straight into this service's own queue, in this transaction: Identity is the sender, so the
            // token never sits in an outbox message or a broker queue. The dispatcher scrubs it once sent.
            await _emails.QueueAsync(new OutgoingEmail
            {
                Id = Guid.CreateVersion7(),
                RecipientId = user.Id,
                Template = EmailTemplate.PasswordReset,
                DataJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> { ["token"] = token }),
                Language = string.IsNullOrWhiteSpace(request.Language) ? EmailTemplates.DefaultLanguage : request.Language,
                Status = OutgoingEmailStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now,
            }, ct);
        }, cancellationToken);
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // One guarded statement decides: used, expired, replaced or never issued all end here, alike.
            var userId = await _resets.TryClaimAsync(ResetTokens.Hash(request.Token), now, ct)
                ?? throw new ValidationException([new ValidationFailure(nameof(request.Token), ResetTokens.Invalid)]);

            var user = await _users.GetByIdAsync(userId, ct)
                ?? throw new ValidationException([new ValidationFailure(nameof(request.Token), ResetTokens.Invalid)]);

            user.PasswordHash = _hasher.HashPassword(request.Password);
            user.UpdatedAt = now;

            await _audit.RecordAsync(AuditCategory.Security, "PasswordReset", "User", user.Id.ToString(),
                $"{user.Email} chose a new password with a reset link; every session ended",
                actor: AuditActors.Of(user), cancellationToken: ct);
            await _users.SaveChangesAsync(ct);

            // Whoever held a session - perhaps the reason the password was reset - holds it no longer.
            await _users.RevokeAllRefreshTokensAsync(user.Id, now, ct);

            // The link proved the mailbox: wrong passwords counted against this email are forgotten (specs/062).
            await _throttle.ClearAsync(EmailKey.For(user.Email), ct);
        }, cancellationToken);
    }
}
