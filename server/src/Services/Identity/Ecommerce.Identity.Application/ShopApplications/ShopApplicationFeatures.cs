using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit;
using MediatR;

namespace Ecommerce.Application.ShopApplications;

/// <summary>An application to sell (specs/044). The applicant's name and email are filled for staff only.</summary>
public record ShopApplicationResponse(
    Guid Id,
    Guid UserId,
    string? ApplicantEmail,
    string? ApplicantName,
    string ShopName,
    string? Description,
    string? Phone,
    string Status,
    string? DecisionReason,
    DateTime CreatedAt,
    DateTime? DecidedAt)
{
    public static ShopApplicationResponse Mine(ShopApplication a) => new(
        a.Id, a.UserId, null, null, a.ShopName, a.Description, a.Phone, a.Status.ToString(), a.DecisionReason, a.CreatedAt, a.DecidedAt);

    public static ShopApplicationResponse ForStaff(ShopApplicationRow r) => new(
        r.Application.Id, r.Application.UserId, r.Email, $"{r.FirstName} {r.LastName}".Trim(),
        r.Application.ShopName, r.Application.Description, r.Application.Phone, r.Application.Status.ToString(),
        r.Application.DecisionReason, r.Application.CreatedAt, r.Application.DecidedAt);
}

public record ShopApplicationPage(List<ShopApplicationResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>A signed-in customer asks to sell (specs/044). Who applies comes from the token.</summary>
public record ApplyForShopCommand(string ShopName, string? Description, string? Phone) : IRequest<ShopApplicationResponse>;

public class ApplyForShopCommandValidator : AbstractValidator<ApplyForShopCommand>
{
    public ApplyForShopCommandValidator()
    {
        RuleFor(x => x.ShopName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Shop name is required.")
            .MaximumLength(100).WithMessage("Shop name must be at most 100 characters.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}

/// <summary>The caller's own applications, newest first.</summary>
public record GetMyShopApplicationsQuery : IRequest<List<ShopApplicationResponse>>;

/// <summary>Staff read the queue (pending, oldest first) or the history.</summary>
public record GetShopApplicationsQuery(string? Status, int Page = 1, int PageSize = 12) : IRequest<ShopApplicationPage>;

public class GetShopApplicationsQueryValidator : AbstractValidator<GetShopApplicationsQuery>
{
    public GetShopApplicationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.Status)
            .Must(s => s is null || Enum.TryParse<ShopApplicationStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be Pending, Approved or Rejected.");
    }
}

public record ApproveShopApplicationCommand(Guid Id) : IRequest<ShopApplicationResponse>;

public record RejectShopApplicationCommand(Guid Id, string Reason) : IRequest<ShopApplicationResponse>;

public class RejectShopApplicationCommandValidator : AbstractValidator<RejectShopApplicationCommand>
{
    public RejectShopApplicationCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

/// <summary>
/// What an applicant may start, shared with <c>register-seller</c> so the two cannot drift: not already a
/// seller, and not already waiting.
/// </summary>
public static class ShopApplicationRules
{
    public static async Task EnsureMayApplyAsync(
        User user, IShopApplicationRepository applications, CancellationToken cancellationToken)
    {
        if (user.Roles.Any(r => r.Name == RoleNames.Seller))
            throw new ConflictException("This account already sells on the shop.");

        if (await applications.HasPendingAsync(user.Id, cancellationToken))
            throw new ConflictException("An application is already waiting for review.");
    }
}

public class ShopApplicationHandlers(
    IShopApplicationRepository applications,
    IUserRepository users,
    IRoleRepository roles,
    ICurrentUser currentUser,
    IPublishEndpoint publishEndpoint,
    IAuditTrail audit,
    INotifier notifier) :
    IRequestHandler<ApplyForShopCommand, ShopApplicationResponse>,
    IRequestHandler<GetMyShopApplicationsQuery, List<ShopApplicationResponse>>,
    IRequestHandler<GetShopApplicationsQuery, ShopApplicationPage>,
    IRequestHandler<ApproveShopApplicationCommand, ShopApplicationResponse>,
    IRequestHandler<RejectShopApplicationCommand, ShopApplicationResponse>
{
    private readonly IShopApplicationRepository _applications = applications;
    private readonly IUserRepository _users = users;
    private readonly IRoleRepository _roles = roles;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;

    public async Task<ShopApplicationResponse> Handle(ApplyForShopCommand request, CancellationToken cancellationToken)
    {
        var userId = CallerId();
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        await ShopApplicationRules.EnsureMayApplyAsync(user, _applications, cancellationToken);

        var application = new ShopApplication
        {
            UserId = userId,
            ShopName = request.ShopName.Trim(),
            Description = Blank(request.Description),
            Phone = Blank(request.Phone),
        };
        await _applications.AddAsync(application, cancellationToken);
        await _audit.RecordAsync(AuditCategory.User, "ShopApplied", "ShopApplication", application.Id.ToString(),
            $"{user.Email} applied to open \"{application.ShopName}\"",
            after: new { application.ShopName, application.Description, application.Phone }, cancellationToken: cancellationToken);

        try
        {
            await _applications.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e) when (IsOnePendingViolation(e))
        {
            // Two tabs at once: the partial unique index let one through.
            throw new ConflictException("An application is already waiting for review.");
        }

        return ShopApplicationResponse.Mine(application);
    }

    public async Task<List<ShopApplicationResponse>> Handle(GetMyShopApplicationsQuery request, CancellationToken cancellationToken) =>
        (await _applications.GetMineAsync(CallerId(), cancellationToken)).Select(ShopApplicationResponse.Mine).ToList();

    public async Task<ShopApplicationPage> Handle(GetShopApplicationsQuery request, CancellationToken cancellationToken)
    {
        ShopApplicationStatus? status = request.Status is null
            ? null
            : Enum.Parse<ShopApplicationStatus>(request.Status, ignoreCase: true);
        var (items, total) = await _applications.GetPageAsync(status, request.Page, request.PageSize, cancellationToken);
        return new ShopApplicationPage(items.Select(ShopApplicationResponse.ForStaff).ToList(), request.Page, request.PageSize, total);
    }

    /// <summary>
    /// Approval makes the applicant a seller (specs/044): the role, the shop, and Catalog told about it -
    /// all in the transaction that moves the application out of Pending, so none of them happens twice.
    /// </summary>
    public async Task<ShopApplicationResponse> Handle(ApproveShopApplicationCommand request, CancellationToken cancellationToken)
    {
        var row = await _applications.GetAsync(request.Id, cancellationToken) ?? throw new NotFoundException("Application not found.");
        var now = DateTime.UtcNow;
        var application = row.Application;

        var decided = await _applications.TryDecideAsync(application.Id, ShopApplicationStatus.Approved, null, CallerId(), now,
            async ct =>
            {
                var user = await _users.GetByIdAsync(application.UserId, ct) ?? throw new NotFoundException("User not found.");
                var seller = await _roles.GetByNameAsync(RoleNames.Seller, ct)
                    ?? throw new InvalidOperationException($"The '{RoleNames.Seller}' role is missing.");

                if (user.Roles.All(r => r.Name != RoleNames.Seller))
                    user.Roles.Add(seller);
                user.UpdatedAt = now;

                await _users.AddSellerProfileAsync(new SellerProfile { UserId = user.Id, ShopName = application.ShopName }, ct);

                // Staged with the approval (constitution III): a seller Catalog never hears about would have
                // their products read as the shop's own forever.
                await _publishEndpoint.Publish(new SellerRegisteredEvent(user.Id, application.ShopName, now), ct);
                await _audit.RecordAsync(AuditCategory.Moderation, "ShopApproved", "ShopApplication", application.Id.ToString(),
                    $"\"{application.ShopName}\" approved for {row.Email}",
                    before: new { Status = "Pending" }, after: new { Status = "Approved", Roles = user.Roles.Select(r => r.Name).OrderBy(n => n) },
                    cancellationToken: ct);
                await _notifier.NotifyAsync(user.Id, NotificationKind.ShopApproved,
                    new Dictionary<string, string> { ["shop"] = application.ShopName }, "/shop", ct);
            }, cancellationToken);

        return await DecidedAsync(application.Id, decided, cancellationToken);
    }

    public async Task<ShopApplicationResponse> Handle(RejectShopApplicationCommand request, CancellationToken cancellationToken)
    {
        var row = await _applications.GetAsync(request.Id, cancellationToken) ?? throw new NotFoundException("Application not found.");
        var reason = request.Reason.Trim();
        var application = row.Application;

        var decided = await _applications.TryDecideAsync(application.Id, ShopApplicationStatus.Rejected, reason, CallerId(), DateTime.UtcNow,
            async ct =>
            {
                await _audit.RecordAsync(AuditCategory.Moderation, "ShopRejected", "ShopApplication", application.Id.ToString(),
                    $"\"{application.ShopName}\" rejected for {row.Email}",
                    before: new { Status = "Pending" }, after: new { Status = "Rejected", Reason = reason }, cancellationToken: ct);
                await _notifier.NotifyAsync(application.UserId, NotificationKind.ShopRejected,
                    new Dictionary<string, string> { ["shop"] = application.ShopName, ["reason"] = reason }, "/open-shop", ct);
            }, cancellationToken);

        return await DecidedAsync(application.Id, decided, cancellationToken);
    }

    private async Task<ShopApplicationResponse> DecidedAsync(Guid id, bool decided, CancellationToken cancellationToken)
    {
        var after = await _applications.GetAsync(id, cancellationToken) ?? throw new NotFoundException("Application not found.");
        if (!decided)
            throw new ConflictException($"This application is already {after.Application.Status.ToString().ToLowerInvariant()}.");
        return ShopApplicationResponse.ForStaff(after);
    }

    private Guid CallerId() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsOnePendingViolation(Exception e) =>
        e.InnerException?.Message.Contains("IX_shop_applications_one_pending", StringComparison.Ordinal) == true;
}
