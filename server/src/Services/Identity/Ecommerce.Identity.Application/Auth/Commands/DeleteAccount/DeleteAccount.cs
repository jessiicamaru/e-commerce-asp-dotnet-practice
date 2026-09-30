using System.Security.Cryptography;
using Ecommerce.Application.Auth.Commands.Account;
using Ecommerce.Application.Auth.SignInThrottling;
using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MassTransit;
using MediatR;
using ValidationException = FluentValidation.ValidationException;

namespace Ecommerce.Application.Auth.Commands.DeleteAccount;

/// <summary>The caller deletes their own account (specs/112, #217) - who, from the token; proven by the password.</summary>
public record DeleteAccountCommand(string Password) : IRequest;

public class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator() => RuleFor(x => x.Password).NotEmpty();
}

/// <summary>What keeps an account open, asked of Order live (specs/112 research D2) with the caller's token.</summary>
public interface IAccountStanding
{
    /// <summary>Zero or more of <c>OpenOrders</c>, <c>OpenReturns</c>, <c>OpenSales</c>, <c>UnpaidEarnings</c>.</summary>
    Task<IReadOnlyList<string>> GetMyBlockersAsync(CancellationToken cancellationToken = default);
}

public interface IAccountErasure
{
    /// <summary>
    /// Deletes every row Identity holds about the person except their user row (specs/112 data-model): addresses, the
    /// seller profile and payout account, shop applications, outgoing emails, sessions, reset and confirmation tokens,
    /// two-factor challenges and recovery codes, and the sign-in counter kept under <paramref name="emailKey"/>.
    /// Runs inside the caller's transaction.
    /// </summary>
    Task EraseAsync(Guid userId, string emailKey, CancellationToken cancellationToken = default);
}

/// <summary>The words and codes of a deletion (specs/112).</summary>
public static class AccountDeletion
{
    /// <summary>The 409 an Admin or Moderator gets (research D5).</summary>
    public const string StaffAccount = "StaffAccount";

    /// <summary>The 409 while business is open; its <c>reasons</c> say which.</summary>
    public const string OpenBusiness = "AccountHasOpenBusiness";

    public const string StaffMessage =
        "A staff account is not deleted from here. A moderator asks an administrator to revoke the role first.";

    public const string OpenBusinessMessage =
        "This account still has business open - orders, returns, sales or earnings not yet paid out - and is not deleted until it is settled.";

    /// <summary>What the email becomes: unique, and at a reserved domain (RFC 2606) that can never receive mail.</summary>
    public static string PlaceholderEmail(Guid userId) => $"deleted-{userId:N}@deleted.invalid";
}

public class DeleteAccountCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ISignInThrottle throttle,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IPublishEndpoint publishEndpoint,
    IAccountStanding standing,
    IAccountErasure erasure) : IRequestHandler<DeleteAccountCommand>
{
    private static readonly string[] StaffRoles = ["Admin", "Moderator"];
    private static readonly string[] Ranked = ["Admin", "Moderator", "Seller", "Customer"];

    private readonly IUserRepository _users = users;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly ISignInThrottle _throttle = throttle;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAccountStanding _standing = standing;
    private readonly IAccountErasure _erasure = erasure;

    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var user = await _users.GetByIdAsync(me, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            throw new NotFoundException("User not found.");
        }

        if (user.Roles.Any(r => StaffRoles.Contains(r.Name)))
        {
            throw new ConflictException(AccountDeletion.StaffMessage,
                new Dictionary<string, object?> { ["code"] = AccountDeletion.StaffAccount });
        }

        // The password proves it is the person, not a stolen access token - and a wrong one counts toward the
        // sign-in pause (specs/062), as it does when changing the password.
        var emailKey = EmailKey.For(user.Email);
        var now = DateTime.UtcNow;
        if (await _throttle.BlockedUntilAsync(emailKey, now, cancellationToken) is { } pausedUntil)
        {
            throw new TooManyRequestsException("Too many wrong passwords for this email. Try again later.", pausedUntil - now);
        }

        if (!_hasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            await _throttle.RecordFailureAsync(emailKey, now, cancellationToken);
            throw new ValidationException([new ValidationFailure(nameof(request.Password), AccountHandlers.WrongPassword)]);
        }

        // Asked before the transaction: a network call inside it would hold it open across a round trip (specs/031).
        var blockers = await _standing.GetMyBlockersAsync(cancellationToken);
        if (blockers.Count > 0)
        {
            throw new ConflictException(AccountDeletion.OpenBusinessMessage,
                new Dictionary<string, object?> { ["code"] = AccountDeletion.OpenBusiness, ["reasons"] = blockers });
        }

        var email = user.Email;
        var role = Ranked.FirstOrDefault(r => user.Roles.Any(held => held.Name == r));

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // No email in the entry, as actor or summary: it is what the deletion takes away (research D7).
            await _audit.RecordAsync(AuditCategory.Security, "AccountDeleted", "User", user.Id.ToString(),
                "An account was deleted", actor: new AuditActor(user.Id, null, role), cancellationToken: ct);
            await _publishEndpoint.Publish(new AccountDeleted(user.Id, email, now), ct);
            // Every session ends within seconds, like a ban (specs/065); the refresh tokens are deleted below.
            await _publishEndpoint.Publish(new AccessTokensRevoked(user.Id, now, "AccountDeleted"), ct);

            await _erasure.EraseAsync(user.Id, emailKey, ct);

            user.Email = AccountDeletion.PlaceholderEmail(user.Id);
            user.FirstName = string.Empty;
            user.LastName = string.Empty;
            user.PhoneNumber = null;
            user.PasswordHash = _hasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            user.TwoFactorSecret = null;
            user.TwoFactorEnabledAt = null;
            user.TwoFactorLastStep = null;
            user.EmailConfirmedAt = null;
            user.Language = null;
            user.Roles.Clear();
            user.DeletedAt = now;
            user.UpdatedAt = now;

            await _users.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
