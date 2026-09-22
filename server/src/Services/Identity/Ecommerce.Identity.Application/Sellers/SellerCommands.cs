using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Contracts.Identity;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit;
using MediatR;

namespace Ecommerce.Application.Sellers;

/// <summary>What a seller's own shop looks like to them (specs/027).</summary>
public record SellerProfileResponse(Guid SellerId, string ShopName, DateTime CreatedAt);

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

        return new SellerProfileResponse(profile.UserId, profile.ShopName, profile.CreatedAt);
    }
}

public class RenameShopCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    IPublishEndpoint publishEndpoint) : IRequestHandler<RenameShopCommand, SellerProfileResponse>
{
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

        profile.ShopName = request.ShopName.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        // Staged, published, then saved: the new name and the announcement commit together, or the
        // catalogue keeps showing a name this database no longer holds.
        await _publishEndpoint.Publish(
            new SellerRenamedEvent(profile.UserId, profile.ShopName, profile.UpdatedAt), cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);

        return new SellerProfileResponse(profile.UserId, profile.ShopName, profile.CreatedAt);
    }
}
