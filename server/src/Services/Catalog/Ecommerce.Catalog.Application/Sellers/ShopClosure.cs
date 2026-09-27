using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Catalog.Application.Common.Models;
using Ecommerce.Catalog.Application.Products.Saved;
using Ecommerce.Catalog.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MediatR;

namespace Ecommerce.Catalog.Application.Sellers;

/// <summary>
/// A shop's state as its seller and staff read it (#214, specs/107). <paramref name="State"/> is worded by precedence -
/// Suspended, Closed, Paused, Open - while both dates are given, so a shop both closed and paused shows both.
/// </summary>
public record ShopStateResponse(
    Guid SellerId, string ShopName, string State, DateTime? PausedAt, DateTime? ClosedAt, string? ClosedReason)
{
    public static ShopStateResponse From(Seller s) => new(
        s.SellerId, s.ShopName,
        s.Suspended ? "Suspended" : s.ClosedAt is not null ? "Closed" : s.PausedAt is not null ? "Paused" : "Open",
        s.PausedAt, s.ClosedAt, s.ClosedReason);
}

/// <summary>The caller's own shop - the seller id is the token's, never a parameter.</summary>
public record GetMyShopQuery : IRequest<ShopStateResponse>;

/// <summary>The seller takes their shop off the shelf - away, not selling. Paid orders still wait for them.</summary>
public record PauseMyShopCommand : IRequest<ShopStateResponse>;

/// <summary>The seller puts their paused shop back. Refused while staff keep it closed.</summary>
public record ResumeMyShopCommand : IRequest<ShopStateResponse>;

/// <summary>Staff close a shop, with the reason its seller reads - the account untouched.</summary>
public record CloseShopCommand(Guid SellerId, string Reason) : IRequest<ShopStateResponse>;

/// <summary>Staff reopen a shop they closed. The seller's own pause, if any, stays.</summary>
public record ReopenShopCommand(Guid SellerId) : IRequest<ShopStateResponse>;

/// <summary>The shops staff closed, newest closure first.</summary>
public record GetClosedShopsQuery(int PageNumber = 1, int PageSize = 12) : IRequest<PaginatedList<ShopStateResponse>>;

public class CloseShopCommandValidator : AbstractValidator<CloseShopCommand>
{
    public CloseShopCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Reason is required.").MaximumLength(500);
}

public class GetClosedShopsQueryValidator : AbstractValidator<GetClosedShopsQuery>
{
    public GetClosedShopsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class ShopClosureHandlers(
    ISellerRepository sellers,
    IProductRepository products,
    ISavedProductRepository saved,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IEmailSender email) :
    IRequestHandler<GetMyShopQuery, ShopStateResponse>,
    IRequestHandler<PauseMyShopCommand, ShopStateResponse>,
    IRequestHandler<ResumeMyShopCommand, ShopStateResponse>,
    IRequestHandler<CloseShopCommand, ShopStateResponse>,
    IRequestHandler<ReopenShopCommand, ShopStateResponse>,
    IRequestHandler<GetClosedShopsQuery, PaginatedList<ShopStateResponse>>
{
    private readonly ISellerRepository _sellers = sellers;
    private readonly IProductRepository _products = products;
    private readonly ISavedProductRepository _saved = saved;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;
    private readonly IEmailSender _email = email;

    public const string NotFound = "Shop not found.";

    public async Task<ShopStateResponse> Handle(GetMyShopQuery request, CancellationToken cancellationToken) =>
        ShopStateResponse.From(await ShopAsync(Me(), cancellationToken));

    public Task<ShopStateResponse> Handle(PauseMyShopCommand request, CancellationToken cancellationToken) =>
        MoveAsync(Me(), ShopMove.Pause, null, cancellationToken);

    public Task<ShopStateResponse> Handle(ResumeMyShopCommand request, CancellationToken cancellationToken) =>
        MoveAsync(Me(), ShopMove.Resume, null, cancellationToken);

    public Task<ShopStateResponse> Handle(CloseShopCommand request, CancellationToken cancellationToken) =>
        MoveAsync(request.SellerId, ShopMove.Close, request.Reason.Trim(), cancellationToken);

    public Task<ShopStateResponse> Handle(ReopenShopCommand request, CancellationToken cancellationToken) =>
        MoveAsync(request.SellerId, ShopMove.Reopen, null, cancellationToken);

    public async Task<PaginatedList<ShopStateResponse>> Handle(GetClosedShopsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _sellers.GetClosedAsync(request.PageNumber, request.PageSize, cancellationToken);
        return new PaginatedList<ShopStateResponse>(
            items.Select(ShopStateResponse.From).ToList(), total, request.PageNumber, request.PageSize);
    }

    /// <summary>
    /// One move: the guarded statement, then - only for the call that won it - the audit entry, the seller's notice and
    /// the savers' notices, in the same transaction. Zero rows is a 409 that says why, read from the row afterwards.
    /// </summary>
    private async Task<ShopStateResponse> MoveAsync(Guid sellerId, ShopMove move, string? reason, CancellationToken cancellationToken)
    {
        var before = await ShopAsync(sellerId, cancellationToken);
        var actor = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var byStaff = move is ShopMove.Close or ShopMove.Reopen;

        var moved = await _sellers.TryMoveShopAsync(sellerId, move, DateTime.UtcNow, reason, byStaff ? actor : null, async ct =>
        {
            var action = move switch
            {
                ShopMove.Pause => "ShopPaused",
                ShopMove.Resume => "ShopResumed",
                ShopMove.Close => "ShopClosed",
                _ => "ShopReopened",
            };
            await _audit.RecordAsync(byStaff ? AuditCategory.Moderation : AuditCategory.Catalog, action, "Shop", sellerId.ToString(),
                $"\"{before.ShopName}\" {action[4..].ToLowerInvariant()}" + (reason is null ? "" : $": {reason}"),
                new { State = ShopStateResponse.From(before).State },
                new { Move = move.ToString(), Reason = reason },
                cancellationToken: ct, aboutUserId: sellerId);

            // The seller learns what staff did to their shop; what they did themselves needs no notice.
            if (move == ShopMove.Close)
                await _notifier.NotifyAsync(sellerId, "ShopClosed", new Dictionary<string, string> { ["reason"] = reason! }, "/shop", ct);
            if (move == ShopMove.Reopen)
                await _notifier.NotifyAsync(sellerId, "ShopReopened", new Dictionary<string, string>(), "/shop", ct);

            // Back on the shelf: each product now on sale is, for whoever saved it, available again (specs/091) - the
            // same notice as a lifted ban. Read inside the transaction, after the shelf flag was recomputed, so a shop
            // another reason keeps shut tells nobody.
            if (move is ShopMove.Resume or ShopMove.Reopen)
            {
                foreach (var product in await _products.GetOnSaleBySellerAsync(sellerId, ct))
                    await SavedProductNotices.BackOnSaleAsync(product, _saved, _notifier, _email, ct);
            }
        }, cancellationToken);

        var after = await ShopAsync(sellerId, cancellationToken);
        if (!moved)
            throw new ConflictException(Why(after, move));

        return ShopStateResponse.From(after);
    }

    private static string Why(Seller shop, ShopMove move) => move switch
    {
        ShopMove.Pause or ShopMove.Resume when shop.ClosedAt is not null => $"Staff closed this shop: {shop.ClosedReason}",
        ShopMove.Pause => "The shop is already paused.",
        ShopMove.Resume => "The shop is not paused.",
        ShopMove.Close => "The shop is already closed.",
        _ => "The shop is not closed.",
    };

    /// <summary>One 404 for "no such seller" and "not heard of its name yet" - the same as the public shop page.</summary>
    private async Task<Seller> ShopAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var shop = await _sellers.GetAsync(sellerId, cancellationToken);
        return shop is null || string.IsNullOrEmpty(shop.ShopName) ? throw new NotFoundException(NotFound) : shop;
    }

    private Guid Me() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
