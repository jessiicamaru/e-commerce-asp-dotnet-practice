using System.Text;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.SignInThrottling;
using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.Account;

/// <summary>The caller's own details (specs/064). The email is read here but not changed here.</summary>
public record AccountProfile(string Email, string FirstName, string LastName, string? Phone, bool EmailConfirmed);

/// <summary>Who is calling - from the token, never from the request (constitution IV).</summary>
public record GetMeQuery : IRequest<AccountProfile>;

/// <summary>Changes the caller's own name and phone (specs/064).</summary>
public record UpdateMeCommand(string FirstName, string LastName, string? Phone) : IRequest<AccountProfile>;

/// <summary>
/// Changes the caller's own password (specs/064). <see cref="KeepRefreshToken"/> is this browser's session,
/// which survives the change; the controller fills it from the HttpOnly cookie, never from the body.
/// </summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest
{
    public string? KeepRefreshToken { get; init; }
}

public class UpdateMeCommandValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required.").MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required.").MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Your current password is required.");

        // The same rules as registration: changing a password must not be the way round them.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(RegisterCommandValidator.MinimumPasswordLength)
            .WithMessage($"Password must be at least {RegisterCommandValidator.MinimumPasswordLength} characters.")
            .Must(p => p is null || Encoding.UTF8.GetByteCount(p) <= RegisterCommandValidator.MaximumPasswordBytes)
            .WithMessage($"Password must be at most {RegisterCommandValidator.MaximumPasswordBytes} bytes.");
    }
}

public class AccountHandlers(
    IUserRepository users,
    IPasswordHasher hasher,
    ISignInThrottle throttle,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditTrail audit) :
    IRequestHandler<GetMeQuery, AccountProfile>,
    IRequestHandler<UpdateMeCommand, AccountProfile>,
    IRequestHandler<ChangePasswordCommand>
{
    public const string WrongPassword = "Your current password is not correct.";

    private readonly IUserRepository _users = users;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly ISignInThrottle _throttle = throttle;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task<AccountProfile> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(CallerId(), cancellationToken) ?? throw new NotFoundException("User not found.");
        return new AccountProfile(user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.EmailConfirmed);
    }

    public async Task<AccountProfile> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(CallerId(), cancellationToken) ?? throw new NotFoundException("User not found.");
        var before = new { user.FirstName, user.LastName, Phone = user.PhoneNumber };

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _audit.RecordAsync(AuditCategory.User, "ProfileUpdated", "User", user.Id.ToString(),
            $"{user.Email} changed their details",
            before: before, after: new { user.FirstName, user.LastName, Phone = user.PhoneNumber },
            actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new AccountProfile(user.Email, user.FirstName, user.LastName, user.PhoneNumber, user.EmailConfirmed);
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(CallerId(), cancellationToken) ?? throw new NotFoundException("User not found.");
        var emailKey = EmailKey.For(user.Email);
        var now = DateTime.UtcNow;

        // The sign-in pause (specs/062) holds here too: a stolen access token must not guess the password
        // faster than the sign-in page allows.
        if (await _throttle.BlockedUntilAsync(emailKey, now, cancellationToken) is { } pausedUntil)
        {
            throw new TooManyRequestsException("Too many wrong passwords for this email. Try again later.", pausedUntil - now);
        }

        if (!_hasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            await _throttle.RecordFailureAsync(emailKey, now, cancellationToken);
            throw new ValidationException([new ValidationFailure(nameof(request.CurrentPassword), WrongPassword)]);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            user.PasswordHash = _hasher.HashPassword(request.NewPassword);
            user.UpdatedAt = now;

            await _audit.RecordAsync(AuditCategory.Security, "PasswordChanged", "User", user.Id.ToString(),
                $"{user.Email} changed their password; every other session ended",
                actor: AuditActors.Of(user), cancellationToken: ct);
            await _users.SaveChangesAsync(ct);

            // Whoever else holds a session - perhaps the reason for the change - holds it no longer. This
            // browser, which just proved the password, keeps its own.
            await _users.RevokeOtherRefreshTokensAsync(user.Id, request.KeepRefreshToken, now, ct);
        }, cancellationToken);

        await _throttle.ClearAsync(emailKey, cancellationToken);
    }

    private Guid CallerId() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
