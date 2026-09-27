using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Application.Sellers;

/// <summary>What a seller's own shop looks like to them (specs/027).</summary>
public record SellerProfileResponse(Guid SellerId, string ShopName, DateTime CreatedAt, string? Description = null)
{
    public static SellerProfileResponse From(Domain.Entities.SellerProfile profile) =>
        new(profile.UserId, profile.ShopName, profile.CreatedAt, profile.Description);
}

/// <summary>The caller's own shop. Never anybody else's - there is no id to pass.</summary>
public record GetMyShopQuery : IRequest<SellerProfileResponse>;

/// <summary>
/// Renames the caller's shop (specs/027).
/// </summary>
/// <remarks>
/// <b>No product is written.</b> That is the whole reason Catalog keeps the name as a read model
/// rather than a copy on each listing: a seller with two hundred products renames their shop once,
/// and the catalogue shows the new name on all of them.
/// </remarks>
public record RenameShopCommand(string ShopName) : IRequest<SellerProfileResponse>;

public class RenameShopCommandValidator : AbstractValidator<RenameShopCommand>
{
    public RenameShopCommandValidator()
    {
        RuleFor(x => x.ShopName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Shop name is required.")
            .MaximumLength(100).WithMessage("Shop name must be at most 100 characters.");
    }
}

public class GetMyShopQueryHandler(IUserRepository users, ICurrentUser currentUser)
    : IRequestHandler<GetMyShopQuery, SellerProfileResponse>
{
    private readonly IUserRepository _users = users;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<SellerProfileResponse> Handle(GetMyShopQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var profile = await _users.GetSellerProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("This account does not sell on the shop.");

        return SellerProfileResponse.From(profile);
    }
}

public class RenameShopCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IPublishEndpoint publishEndpoint,
    IAuditTrail audit) : IRequestHandler<RenameShopCommand, SellerProfileResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IUserRepository _users = users;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<SellerProfileResponse> Handle(RenameShopCommand request, CancellationToken cancellationToken)
    {
        // Whose shop comes from the TOKEN, never from the body. A request that named a seller would
        // be the defect specs/009 and issue #18 both were, on a third field.
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var profile = await _users.GetSellerProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("This account does not sell on the shop.");

        var before = new { profile.ShopName };
        profile.ShopName = request.ShopName.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        // Staged, published, then saved: the new name and the announcement commit together, or the
        // catalogue keeps showing a name this database no longer holds.
        await _publishEndpoint.Publish(
            new SellerRenamedEvent(profile.UserId, profile.ShopName, profile.UpdatedAt), cancellationToken);

        await _audit.RecordAsync(
            AuditCategory.User, "ShopRenamed", "Seller", profile.UserId.ToString(),
            $"Shop renamed to \"{profile.ShopName}\"", before, new { profile.ShopName },
            cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return SellerProfileResponse.From(profile);
    }
}

/// <summary>
/// The caller's shop described in their own words, or the words cleared (#197, specs/099). Not moderated, like the name:
/// a seller's words about their own shop, shown to shoppers as text.
/// </summary>
public record DescribeShopCommand(string? Description) : IRequest<SellerProfileResponse>;

public class DescribeShopCommandValidator : AbstractValidator<DescribeShopCommand>
{
    public DescribeShopCommandValidator() =>
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("A shop description is at most 500 characters.");
}

public class DescribeShopCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IPublishEndpoint publishEndpoint,
    IAuditTrail audit) : IRequestHandler<DescribeShopCommand, SellerProfileResponse>
{
    private readonly IUserRepository _users = users;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAuditTrail _audit = audit;

    public async Task<SellerProfileResponse> Handle(DescribeShopCommand request, CancellationToken cancellationToken)
    {
        // Whose shop comes from the token, as for the name.
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var profile = await _users.GetSellerProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException("This account does not sell on the shop.");

        var before = new { profile.Description };
        profile.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        // With the save, so Catalog's copy and this row cannot disagree for long (Principle III).
        await _publishEndpoint.Publish(
            new SellerDescribedEvent(profile.UserId, profile.Description, profile.UpdatedAt), cancellationToken);
        await _audit.RecordAsync(
            AuditCategory.User, "ShopDescribed", "Seller", profile.UserId.ToString(), "Shop description changed",
            before, new { profile.Description }, cancellationToken: cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return SellerProfileResponse.From(profile);
    }
}
