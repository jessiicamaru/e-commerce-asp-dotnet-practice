using MassTransit;
using Ecommerce.Contracts.Identity;
using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MediatR;

namespace Ecommerce.Application.Users;

/// <summary>A person as staff see them (specs/043): who, what they hold, and whether they are stopped.</summary>
public record UserAdminResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    List<string> Roles,
    DateTime CreatedAt,
    DateTime? LockedUntil,
    string? LockReason,
    DateTime? BannedAt,
    string? BanReason)
{
    public static UserAdminResponse From(User user, DateTime now) => new(
        user.Id, user.Email, user.FirstName, user.LastName,
        user.Roles.Select(r => r.Name).OrderBy(n => n).ToList(),
        user.CreatedAt,
        // A lock that has run out is no lock: shown as nothing rather than as a date in the past.
        user.IsLocked(now) ? user.LockedUntil : null,
        user.IsLocked(now) ? user.LockReason : null,
        user.BannedAt, user.BanReason);
}

public record UserAdminPage(List<UserAdminResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>Staff look people up by email or name (specs/043). Paged, newest first.</summary>
public record GetUsersQuery(string? Search, int Page = 1, int PageSize = 12) : IRequest<UserAdminPage>;

public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Search).MaximumLength(255);
    }
}

public class GetUsersQueryHandler(IUserRepository users) : IRequestHandler<GetUsersQuery, UserAdminPage>
{
    private readonly IUserRepository _users = users;

    public async Task<UserAdminPage> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _users.SearchAsync(request.Search, request.Page, request.PageSize, cancellationToken);
        var now = DateTime.UtcNow;
        return new UserAdminPage(items.Select(u => UserAdminResponse.From(u, now)).ToList(), request.Page, request.PageSize, total);
    }
}

/// <summary>
/// An administrator gives somebody a role (specs/043). <b>Moderator is the only one</b>: Admin would be a
/// second bootstrap path, and Seller comes from opening a shop.
/// </summary>
public record GrantRoleCommand(Guid UserId, string Role) : IRequest<UserAdminResponse>;

public record RevokeRoleCommand(Guid UserId, string Role) : IRequest<UserAdminResponse>;

public class GrantRoleCommandValidator : AbstractValidator<GrantRoleCommand>
{
    public GrantRoleCommandValidator() =>
        RuleFor(x => x.Role).Equal(RoleNames.Moderator).WithMessage("Only the Moderator role can be granted here.");
}

public class RevokeRoleCommandValidator : AbstractValidator<RevokeRoleCommand>
{
    public RevokeRoleCommandValidator() =>
        RuleFor(x => x.Role).Equal(RoleNames.Moderator).WithMessage("Only the Moderator role can be revoked here.");
}

/// <summary>
/// Stops an account until a date, with a reason (specs/043). Staff; a moderator for at most
/// <see cref="ModerationRules.ModeratorMaxLockDays"/> days.
/// </summary>
public record LockUserCommand(Guid UserId, int Days, string Reason) : IRequest<UserAdminResponse>;

public record UnlockUserCommand(Guid UserId) : IRequest<UserAdminResponse>;

/// <summary>Stops an account until an administrator lifts it (specs/043). Administrators only.</summary>
public record BanUserCommand(Guid UserId, string Reason) : IRequest<UserAdminResponse>;

public record LiftBanCommand(Guid UserId) : IRequest<UserAdminResponse>;

public class LockUserCommandValidator : AbstractValidator<LockUserCommand>
{
    public LockUserCommandValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, ModerationRules.AdminMaxLockDays);
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
    }
}

public class BanUserCommandValidator : AbstractValidator<BanUserCommand>
{
    public BanUserCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

/// <summary>
/// Who may do what to whom (specs/043). The controller's attributes decide who reaches an endpoint; these
/// decide the rest, because they depend on the target's row.
/// </summary>
public static class ModerationRules
{
    public const int ModeratorMaxLockDays = 30;
    public const int AdminMaxLockDays = 365;

    /// <summary>Nobody stops themselves or an administrator; a moderator does not stop another moderator.</summary>
    public static void EnsureMayStop(ICurrentUser caller, User target)
    {
        if (target.Id == caller.Id)
            throw new ConflictException("You cannot lock or ban your own account.");

        if (target.Roles.Any(r => r.Name == RoleNames.Admin))
            throw new ConflictException("An administrator cannot be locked or banned.");

        if (!caller.IsInRole(RoleNames.Admin) && target.Roles.Any(r => r.Name == RoleNames.Moderator))
            throw new ForbiddenException("Only an administrator can lock a moderator.");
    }

    /// <summary>
    /// Unlocking obeys what locking does (#121, specs/050): nobody frees themselves - a lock ends every
    /// session but not an access token already in hand (#112) - a moderator does not free a moderator, and
    /// a lock with more time to run than a moderator could set is an administrator's to lift.
    /// </summary>
    /// <remarks>
    /// "Could have set" is read from the time still to run, not from who set it: that needs no column,
    /// and a long lock stays until an administrator decides (spec, Decision).
    /// </remarks>
    public static void EnsureMayRelease(ICurrentUser caller, User target, DateTime now)
    {
        if (target.Id == caller.Id)
            throw new ConflictException("You cannot unlock your own account.");

        if (caller.IsInRole(RoleNames.Admin))
            return;

        if (target.Roles.Any(r => r.Name == RoleNames.Moderator))
            throw new ForbiddenException("Only an administrator can unlock a moderator.");

        if (target.LockedUntil is { } until && until - now > TimeSpan.FromDays(ModeratorMaxLockDays))
            throw new ForbiddenException($"Only an administrator can lift a lock with more than {ModeratorMaxLockDays} days to run.");
    }

    /// <summary>
    /// Locking again replaces the end date, so a lock that ends sooner than the one in place lifts part of it
    /// (#180, specs/088) - and obeys the unlock rule's reach: a moderator shortens only a lock with at most
    /// <see cref="ModeratorMaxLockDays"/> days still to run. Extending is always a lock the caller could set.
    /// </summary>
    /// <remarks>Runs after <see cref="EnsureMayStop"/>, which has already refused self and a moderator's moderator.</remarks>
    public static void EnsureMayShorten(ICurrentUser caller, User target, DateTime newUntil, DateTime now)
    {
        if (caller.IsInRole(RoleNames.Admin) || target.LockedUntil is not { } until || newUntil >= until)
            return;

        if (until - now > TimeSpan.FromDays(ModeratorMaxLockDays))
            throw new ForbiddenException($"Only an administrator can shorten a lock with more than {ModeratorMaxLockDays} days to run.");
    }
}

public class UserAdministrationHandlers(
    IUserRepository users,
    IRoleRepository roles,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IPublishEndpoint publishEndpoint,
    IEmailSender email) :
    IRequestHandler<GrantRoleCommand, UserAdminResponse>,
    IRequestHandler<RevokeRoleCommand, UserAdminResponse>,
    IRequestHandler<LockUserCommand, UserAdminResponse>,
    IRequestHandler<UnlockUserCommand, UserAdminResponse>,
    IRequestHandler<BanUserCommand, UserAdminResponse>,
    IRequestHandler<LiftBanCommand, UserAdminResponse>
{
    private readonly IUserRepository _users = users;
    private readonly IRoleRepository _roles = roles;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IEmailSender _email = email;

    public async Task<UserAdminResponse> Handle(GrantRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await TargetAsync(request.UserId, cancellationToken);
        if (user.IsBanned)
            throw new ConflictException("A banned account cannot be given a role.");

        if (user.Roles.All(r => r.Name != request.Role))
        {
            var before = Snapshot(user);
            var role = await _roles.GetByNameAsync(request.Role, cancellationToken)
                ?? throw new InvalidOperationException($"The '{request.Role}' role is missing.");
            user.Roles.Add(role);
            user.UpdatedAt = DateTime.UtcNow;

            await _audit.RecordAsync(AuditCategory.Security, "RoleGranted", "User", user.Id.ToString(),
                $"{user.Email} was made {request.Role}", before, Snapshot(user), cancellationToken: cancellationToken);
            await _notifier.NotifyAsync(user.Id, NotificationKind.ModeratorGranted, link: "/admin", cancellationToken: cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
        }

        return UserAdminResponse.From(user, DateTime.UtcNow);
    }

    public async Task<UserAdminResponse> Handle(RevokeRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await TargetAsync(request.UserId, cancellationToken);
        var held = user.Roles.FirstOrDefault(r => r.Name == request.Role);

        if (held is not null)
        {
            var before = Snapshot(user);
            user.Roles.Remove(held);
            user.UpdatedAt = DateTime.UtcNow;

            await _audit.RecordAsync(AuditCategory.Security, "RoleRevoked", "User", user.Id.ToString(),
                $"{user.Email} is no longer {request.Role}", before, Snapshot(user), cancellationToken: cancellationToken);
            await _notifier.NotifyAsync(user.Id, NotificationKind.ModeratorRevoked, cancellationToken: cancellationToken);
            // The role leaves the tokens already issued within seconds, not at their next refresh (specs/065).
            await _publishEndpoint.Publish(new AccessTokensRevoked(user.Id, user.UpdatedAt, "RoleRevoked"), cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
        }

        return UserAdminResponse.From(user, DateTime.UtcNow);
    }

    public async Task<UserAdminResponse> Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(RoleNames.Admin) && request.Days > ModerationRules.ModeratorMaxLockDays)
            throw new ForbiddenException($"A moderator can lock an account for at most {ModerationRules.ModeratorMaxLockDays} days.");

        var user = await TargetAsync(request.UserId, cancellationToken);
        ModerationRules.EnsureMayStop(_currentUser, user);

        var now = DateTime.UtcNow;
        var until = now.AddDays(request.Days);
        ModerationRules.EnsureMayShorten(_currentUser, user, until, now);

        var before = Snapshot(user);
        user.LockedUntil = until;
        user.LockReason = request.Reason.Trim();
        user.UpdatedAt = now;

        await _audit.RecordAsync(AuditCategory.Moderation, "AccountLocked", "User", user.Id.ToString(),
            $"{user.Email} locked for {request.Days} day(s)", before, Snapshot(user), cancellationToken: cancellationToken);
        // Unreadable while the lock lasts - the reason is shown at sign-in (specs/049) - and afterwards their
        // record of what happened and why (#128, specs/059).
        await _notifier.NotifyAsync(user.Id, NotificationKind.AccountLocked,
            new Dictionary<string, string>
            {
                ["until"] = DateTime.SpecifyKind(user.LockedUntil.Value, DateTimeKind.Utc).ToString("o"),
                ["reason"] = user.LockReason,
            }, cancellationToken: cancellationToken);
        // And by email (specs/083): signed out, the bell is the one place they cannot look.
        await _email.SendAsync(user.Id, EmailTemplate.AccountLocked,
            new Dictionary<string, string>
            {
                ["until"] = DateTime.SpecifyKind(user.LockedUntil.Value, DateTimeKind.Utc).ToString("o"),
                ["reason"] = user.LockReason,
            }, EmailTemplate.ReadersLanguage, cancellationToken);
        // Every token already issued stops working within seconds, everywhere (specs/065) - with the lock.
        await _publishEndpoint.Publish(new AccessTokensRevoked(user.Id, now, "Locked"), cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
        // Every session ends now, not when its refresh token would have run out.
        await _users.RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);

        return UserAdminResponse.From(user, now);
    }

    public async Task<UserAdminResponse> Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await TargetAsync(request.UserId, cancellationToken);
        var now = DateTime.UtcNow;
        ModerationRules.EnsureMayRelease(_currentUser, user, now);

        if (user.LockedUntil is not null)
        {
            var before = Snapshot(user);
            user.LockedUntil = null;
            user.LockReason = null;
            user.UpdatedAt = now;

            await _audit.RecordAsync(AuditCategory.Moderation, "AccountUnlocked", "User", user.Id.ToString(),
                $"{user.Email} unlocked", before, Snapshot(user), cancellationToken: cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
        }

        return UserAdminResponse.From(user, now);
    }

    public async Task<UserAdminResponse> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await TargetAsync(request.UserId, cancellationToken);
        ModerationRules.EnsureMayStop(_currentUser, user);

        var now = DateTime.UtcNow;
        var before = Snapshot(user);
        user.BannedAt = now;
        user.BanReason = request.Reason.Trim();
        user.UpdatedAt = now;

        await _audit.RecordAsync(AuditCategory.Moderation, "AccountBanned", "User", user.Id.ToString(),
            $"{user.Email} banned", before, Snapshot(user), cancellationToken: cancellationToken);
        await _notifier.NotifyAsync(user.Id, NotificationKind.AccountBanned,
            new Dictionary<string, string> { ["reason"] = user.BanReason! }, cancellationToken: cancellationToken);
        await _email.SendAsync(user.Id, EmailTemplate.AccountBanned,
            new Dictionary<string, string> { ["reason"] = user.BanReason! }, EmailTemplate.ReadersLanguage, cancellationToken);
        await _publishEndpoint.Publish(new AccessTokensRevoked(user.Id, now, "Banned"), cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);
        await _users.RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);

        return UserAdminResponse.From(user, now);
    }

    public async Task<UserAdminResponse> Handle(LiftBanCommand request, CancellationToken cancellationToken)
    {
        var user = await TargetAsync(request.UserId, cancellationToken);
        var now = DateTime.UtcNow;

        if (user.BannedAt is not null)
        {
            var before = Snapshot(user);
            user.BannedAt = null;
            user.BanReason = null;
            user.UpdatedAt = now;

            await _audit.RecordAsync(AuditCategory.Moderation, "BanLifted", "User", user.Id.ToString(),
                $"{user.Email} ban lifted", before, Snapshot(user), cancellationToken: cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);
        }

        return UserAdminResponse.From(user, now);
    }

    private async Task<User> TargetAsync(Guid id, CancellationToken cancellationToken) =>
        await _users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("User not found.");

    /// <summary>What the audit diff compares: the roles and the two stops, nothing personal.</summary>
    private static object Snapshot(User user) => new
    {
        Roles = user.Roles.Select(r => r.Name).OrderBy(n => n).ToList(),
        user.LockedUntil,
        user.LockReason,
        user.BannedAt,
        user.BanReason
    };
}

/// <summary>Who an id is, for an administrator's report (specs/047) - Order knows buyers only by id.</summary>
public record UserBrief(Guid Id, string Email, string FirstName, string LastName);

public record LookupUsersQuery(List<Guid> Ids) : IRequest<List<UserBrief>>;

public class LookupUsersQueryValidator : AbstractValidator<LookupUsersQuery>
{
    public LookupUsersQueryValidator() =>
        RuleFor(x => x.Ids).NotNull().Must(ids => ids.Count is > 0 and <= 100).WithMessage("Between 1 and 100 ids.");
}

/// <summary>The headline numbers about people (specs/047).</summary>
public record UserStats(int Total, int Customers, int Sellers, int Moderators, int Admins, int Locked, int Banned);

public record GetUserStatsQuery : IRequest<UserStats>;

public class UserReportHandlers(IUserRepository users) :
    IRequestHandler<LookupUsersQuery, List<UserBrief>>,
    IRequestHandler<GetUserStatsQuery, UserStats>
{
    private readonly IUserRepository _users = users;

    public async Task<List<UserBrief>> Handle(LookupUsersQuery request, CancellationToken cancellationToken) =>
        (await _users.GetByIdsAsync(request.Ids, cancellationToken))
            .Select(u => new UserBrief(u.Id, u.Email, u.FirstName, u.LastName))
            .ToList();

    public async Task<UserStats> Handle(GetUserStatsQuery request, CancellationToken cancellationToken)
    {
        var (byRole, locked, banned, total) = await _users.CountAsync(DateTime.UtcNow, cancellationToken);
        int Of(string role) => byRole.TryGetValue(role, out var n) ? n : 0;
        return new UserStats(total, Of(RoleNames.Customer), Of(RoleNames.Seller), Of(RoleNames.Moderator), Of(RoleNames.Admin), locked, banned);
    }
}
