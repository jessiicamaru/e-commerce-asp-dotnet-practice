using Ecommerce.Application.Auth.Commands.PasswordReset;
using Ecommerce.Application.Auth.Common;
using Ecommerce.Application.Auth.TwoFactor;
using Ecommerce.Application.Common;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Application.Auth.Handoff;

/// <summary>Where handoffs are kept (#279, specs/140).</summary>
public interface IBackOfficeHandoffRepository
{
    Task AddAsync(BackOfficeHandoff handoff, CancellationToken cancellationToken = default);

    /// <summary>
    /// Redeems a handoff in one guarded statement - unused, unexpired - and answers whose it was, or null. Of two
    /// redemptions at once, exactly one gets an answer.
    /// </summary>
    Task<Guid?> TryClaimAsync(string codeHash, DateTime now, CancellationToken cancellationToken = default);
}

/// <summary>A signed-in staff member asks for a handoff to the back office (specs/140).</summary>
public record IssueBackOfficeHandoffCommand : IRequest<BackOfficeHandoffResponse>;

public record BackOfficeHandoffResponse(string Code);

/// <summary>The back office redeems a handoff for a two-factor challenge - never for a session.</summary>
public record RedeemBackOfficeHandoffCommand(string Code) : IRequest<AuthResponse>;

public class RedeemBackOfficeHandoffCommandValidator : AbstractValidator<RedeemBackOfficeHandoffCommand>
{
    public RedeemBackOfficeHandoffCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}

public static class BackOfficeHandoffs
{
    public const string Invalid = "This link to the back office is invalid or has expired. Sign in instead.";
}

public class BackOfficeHandoffHandlers(
    IUserRepository users,
    IBackOfficeHandoffRepository handoffs,
    ITwoFactorRepository twoFactor,
    ICurrentUser currentUser,
    IAuditTrail audit,
    TimeProvider clock) :
    IRequestHandler<IssueBackOfficeHandoffCommand, BackOfficeHandoffResponse>,
    IRequestHandler<RedeemBackOfficeHandoffCommand, AuthResponse>
{
    private readonly IUserRepository _users = users;
    private readonly IBackOfficeHandoffRepository _handoffs = handoffs;
    private readonly ITwoFactorRepository _twoFactor = twoFactor;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly TimeProvider _clock = clock;

    public async Task<BackOfficeHandoffResponse> Handle(IssueBackOfficeHandoffCommand request, CancellationToken cancellationToken)
    {
        var id = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var user = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        // The back office is for staff (specs/136), and its session needs the second factor (specs/110): a handoff for
        // anybody else would lead to a dead end, so none is issued.
        if (!SessionRoles.IsStaff(user))
            throw new ForbiddenException("Only staff have a back office.", new Dictionary<string, object?> { ["code"] = "NotStaff" });
        if (!user.TwoFactorEnabled)
            throw new ForbiddenException("Set up two-factor sign-in first.", new Dictionary<string, object?> { ["code"] = "TwoFactorSetupRequired" });

        var now = _clock.GetUtcNow().UtcDateTime;
        var code = ResetTokens.NewToken();
        await _handoffs.AddAsync(new BackOfficeHandoff
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            CodeHash = ResetTokens.Hash(code),
            ExpiresAt = now.Add(BackOfficeHandoff.Lifetime),
            CreatedAt = now,
        }, cancellationToken);
        await _audit.RecordAsync(AuditCategory.Security, "BackOfficeHandoffIssued", "User", user.Id.ToString(),
            $"{user.Email} crossed from the storefront to the back office", actor: AuditActors.Of(user), cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new BackOfficeHandoffResponse(code);
    }

    public async Task<AuthResponse> Handle(RedeemBackOfficeHandoffCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var userId = await _handoffs.TryClaimAsync(ResetTokens.Hash(request.Code.Trim()), now, cancellationToken);
        var user = userId is { } id ? await _users.GetByIdAsync(id, cancellationToken) : null;

        // Used, expired, made up - or an account stopped, or its second factor turned off, in the seconds since: one
        // answer, and the back office shows its ordinary sign-in, which says the rest.
        if (user is null || user.IsBanned || user.IsLocked(now) || !user.TwoFactorEnabled)
            throw TwoFactorCodes.Invalid(nameof(request.Code), BackOfficeHandoffs.Invalid);

        // The password's place, not the code's (research D1): a challenge, exactly as the right password gives one.
        var challenge = TwoFactorCodes.NewChallenge();
        await _twoFactor.AddChallengeAsync(new TwoFactorChallenge
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = TwoFactorCodes.Hash(challenge),
            ExpiresAt = now.Add(TwoFactorChallenge.Lifetime),
        }, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new AuthResponse(user.Id, user.Email, string.Empty, string.Empty, string.Empty, string.Empty, [],
            user.EmailConfirmed, TwoFactor: SessionRoles.Required, Challenge: challenge, StaffAccount: true);
    }
}
