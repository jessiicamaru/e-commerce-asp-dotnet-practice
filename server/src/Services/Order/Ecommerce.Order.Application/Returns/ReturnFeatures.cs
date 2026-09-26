using System.Globalization;
using Ecommerce.Contracts.Order;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Application.Returns;

public sealed class ReturnOptions
{
    public const string SectionName = "Returns";

    /// <summary>
    /// Days after delivery a parcel may be returned - and, since specs/066, before a seller's money for it is
    /// due: a return can only start while nothing has been paid for the parcel, so it never makes a debt.
    /// </summary>
    public int WindowDays { get; set; } = 7;

    public TimeSpan Window => TimeSpan.FromDays(WindowDays);
}

/// <summary>What a return is at a glance - for the buyer, the seller and staff (specs/066).</summary>
public record ReturnResponse(
    Guid Id,
    Guid OrderId,
    Guid ShipmentId,
    bool IsShop,
    string Status,
    string Reason,
    string? DecisionReason,
    string? TrackingReference,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    DateTime? SentBackAt,
    DateTime? ReceivedAt,
    decimal? RefundAmount)
{
    public static ReturnResponse From(ParcelReturn r) => new(
        r.Id, r.OrderId, r.ShipmentId, r.SellerId is null, r.Status.ToString(), r.Reason, r.DecisionReason,
        r.TrackingReference, r.RequestedAt, r.DecidedAt, r.SentBackAt, r.ReceivedAt, r.RefundAmount);
}

public record ReturnPage(List<ReturnResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>The parcel a return is about, as Order knows it: whose, delivered when, its lines and currency.</summary>
public record ReturnParcel(
    Guid OrderId,
    Guid ShipmentId,
    Guid BuyerId,
    Guid? SellerId,
    OrderStatus OrderStatus,
    ShipmentStatus ShipmentStatus,
    DateTime? DeliveredAt,
    string Currency,
    List<ReturnParcelLine> Lines)
{
    /// <summary>
    /// What was paid for the goods: their price less what vouchers took off (specs/069), plus their tax - all as
    /// frozen at checkout (specs/012). The delivery is not refunded.
    /// </summary>
    public decimal RefundAmount => Lines.Sum(l => l.UnitPrice * l.Quantity - l.Discount + (l.TaxAmount ?? 0m));
}

/// <param name="Discount">What vouchers took off the line, the seller's and the platform's (specs/069).</param>
public record ReturnParcelLine(Guid SellableId, int Quantity, decimal UnitPrice, decimal? TaxAmount, decimal Discount = 0m);

/// <summary>One step of a return: from which states, to which, and what it writes on the way.</summary>
public record ReturnMove(
    Guid ReturnId,
    ReturnStatus[] From,
    ReturnStatus To,
    DateTime At,
    DateTime? DecidedAfter = null,
    string? DecisionReason = null,
    string? TrackingReference = null,
    decimal? RefundAmount = null);

public interface IReturnRepository
{
    /// <summary>The parcel of this order, if it is <paramref name="buyerId"/>'s (null: anybody's, for staff).</summary>
    Task<ReturnParcel?> GetParcelAsync(Guid orderId, Guid shipmentId, Guid? buyerId, CancellationToken cancellationToken = default);

    /// <summary>The seller's part of this order, if they have one.</summary>
    Task<ReturnParcel?> GetSellerParcelAsync(Guid orderId, Guid sellerId, CancellationToken cancellationToken = default);

    Task<ParcelReturn?> GetForShipmentAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the request once - <c>ON CONFLICT ("ShipmentId") DO NOTHING</c> - and runs <paramref name="stage"/>
    /// with it in the same transaction. False when the parcel already has a return.
    /// </summary>
    Task<bool> TryRequestAsync(ParcelReturn request, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    /// <summary>
    /// ONE guarded statement - from one of <see cref="ReturnMove.From"/>, decided after
    /// <see cref="ReturnMove.DecidedAfter"/> when given - and, when it moved the row, <paramref name="stage"/> in
    /// the same transaction. False when it moved nothing.
    /// </summary>
    Task<bool> TryMoveAsync(ReturnMove move, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);

    Task<(List<ReturnResponse> Items, int TotalCount)> GetPageAsync(ReturnStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
}

// ------------------------------------------------------------------------------------------ commands

/// <summary>The buyer asks to return a whole delivered parcel (specs/066). The buyer is the token's subject.</summary>
public record RequestReturnCommand(Guid OrderId, Guid ShipmentId, string Reason) : IRequest<ReturnResponse>;

/// <summary>The buyer asks staff to look again at a refused return.</summary>
public record EscalateReturnCommand(Guid OrderId, Guid ShipmentId) : IRequest<ReturnResponse>;

/// <summary>The buyer has sent an accepted return back.</summary>
public record SendReturnBackCommand(Guid OrderId, Guid ShipmentId, string TrackingReference) : IRequest<ReturnResponse>;

/// <summary>The seller accepts or refuses a return of their parcel of <paramref name="OrderId"/> - from the token.</summary>
public record DecideSaleReturnCommand(Guid OrderId, bool Accept, string? Reason) : IRequest<ReturnResponse>;

/// <summary>The seller has their parcel back.</summary>
public record ReceiveSaleReturnCommand(Guid OrderId) : IRequest<ReturnResponse>;

/// <summary>
/// An administrator decides: the shop's own parcel, or - for any parcel - a return the buyer escalated, where
/// a refusal is final (Rejected).
/// </summary>
public record DecideReturnCommand(Guid OrderId, Guid ShipmentId, bool Accept, string? Reason) : IRequest<ReturnResponse>;

/// <summary>An administrator has the shop's own parcel back.</summary>
public record ReceiveReturnCommand(Guid OrderId, Guid ShipmentId) : IRequest<ReturnResponse>;

/// <summary>Returns for staff, by state - the escalated ones are the queue (specs/066).</summary>
public record GetReturnsQuery(string? Status, int Page = 1, int PageSize = 12) : IRequest<ReturnPage>;

public class RequestReturnCommandValidator : AbstractValidator<RequestReturnCommand>
{
    public RequestReturnCommandValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why you want to return it.").MaximumLength(1000);
}

public class SendReturnBackCommandValidator : AbstractValidator<SendReturnBackCommand>
{
    public SendReturnBackCommandValidator() =>
        RuleFor(x => x.TrackingReference).NotEmpty().WithMessage("The tracking reference of the parcel you sent is required.").MaximumLength(100);
}

public class DecideSaleReturnCommandValidator : AbstractValidator<DecideSaleReturnCommand>
{
    public DecideSaleReturnCommandValidator() =>
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Accept).WithMessage("A refusal needs a reason the buyer can read.").MaximumLength(500);
}

public class DecideReturnCommandValidator : AbstractValidator<DecideReturnCommand>
{
    public DecideReturnCommandValidator() =>
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Accept).WithMessage("A refusal needs a reason the buyer can read.").MaximumLength(500);
}

public class GetReturnsQueryValidator : AbstractValidator<GetReturnsQuery>
{
    public GetReturnsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || Enum.TryParse<ReturnStatus>(s, true, out _)).WithMessage("Unknown return status.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public static class Returns
{
    public const string NotFound = "Parcel not found.";
    public const string NoReturn = "This parcel has no return.";
}

public class ReturnHandlers(
    IReturnRepository returns,
    ICurrentUser currentUser,
    IOptions<ReturnOptions> options,
    IPublishEndpoint publish,
    IAuditTrail audit,
    INotifier notifier) :
    IRequestHandler<RequestReturnCommand, ReturnResponse>,
    IRequestHandler<EscalateReturnCommand, ReturnResponse>,
    IRequestHandler<SendReturnBackCommand, ReturnResponse>,
    IRequestHandler<DecideSaleReturnCommand, ReturnResponse>,
    IRequestHandler<ReceiveSaleReturnCommand, ReturnResponse>,
    IRequestHandler<DecideReturnCommand, ReturnResponse>,
    IRequestHandler<ReceiveReturnCommand, ReturnResponse>,
    IRequestHandler<GetReturnsQuery, ReturnPage>
{
    private readonly IReturnRepository _returns = returns;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly TimeSpan _window = options.Value.Window;
    private readonly IPublishEndpoint _publish = publish;
    private readonly IAuditTrail _audit = audit;
    private readonly INotifier _notifier = notifier;

    // ---------------------------------------------------------------------------------- the buyer

    public async Task<ReturnResponse> Handle(RequestReturnCommand request, CancellationToken cancellationToken)
    {
        var parcel = await _returns.GetParcelAsync(request.OrderId, request.ShipmentId, CallerId(), cancellationToken)
            ?? throw new NotFoundException(Returns.NotFound);
        var now = DateTime.UtcNow;

        if (parcel.OrderStatus == OrderStatus.Cancelled || parcel.ShipmentStatus != ShipmentStatus.Shipped || parcel.DeliveredAt is null)
            throw new ConflictException("Only a delivered parcel can be returned.");
        if (parcel.DeliveredAt.Value <= now - _window)
            throw new ConflictException($"A parcel can be returned within {_window.Days} days of its delivery; this one arrived on {parcel.DeliveredAt:yyyy-MM-dd}.");

        var row = new ParcelReturn
        {
            Id = Guid.CreateVersion7(),
            OrderId = parcel.OrderId,
            ShipmentId = parcel.ShipmentId,
            CustomerId = parcel.BuyerId,
            SellerId = parcel.SellerId,
            Status = ReturnStatus.Requested,
            Reason = request.Reason.Trim(),
            RequestedAt = now,
            UpdatedAt = now,
        };

        var requested = await _returns.TryRequestAsync(row, async ct =>
        {
            await AuditAsync(row, "ReturnRequested", $"The buyer asked to return a parcel: {row.Reason}", ct);
            if (parcel.SellerId is { } seller)
                await _notifier.NotifyAsync(seller, NotificationKind.ReturnRequested, About(parcel.OrderId), $"/shop/sales/{parcel.OrderId}", ct);
        }, cancellationToken);

        if (!requested)
            throw new ConflictException("This parcel already has a return.");

        return ReturnResponse.From(row);
    }

    public async Task<ReturnResponse> Handle(EscalateReturnCommand request, CancellationToken cancellationToken)
    {
        var (parcel, current) = await MineAsync(request.OrderId, request.ShipmentId, cancellationToken);
        var now = DateTime.UtcNow;

        var moved = await _returns.TryMoveAsync(
            new ReturnMove(current.Id, [ReturnStatus.Refused], ReturnStatus.Escalated, now, DecidedAfter: now - _window),
            ct => AuditAsync(current, "ReturnEscalated", "The buyer asked staff to look again at a refused return", ct),
            cancellationToken);

        if (!moved)
            throw new ConflictException(current.Status == ReturnStatus.Refused
                ? $"A refusal can be taken further within {_window.Days} days."
                : "Only a refused return can be taken further.");

        return await ReadAsync(parcel.ShipmentId, cancellationToken);
    }

    public async Task<ReturnResponse> Handle(SendReturnBackCommand request, CancellationToken cancellationToken)
    {
        var (parcel, current) = await MineAsync(request.OrderId, request.ShipmentId, cancellationToken);
        var now = DateTime.UtcNow;
        var tracking = request.TrackingReference.Trim();

        var moved = await _returns.TryMoveAsync(
            new ReturnMove(current.Id, [ReturnStatus.Accepted], ReturnStatus.SentBack, now, DecidedAfter: now - _window, TrackingReference: tracking),
            async ct =>
            {
                await AuditAsync(current, "ReturnSentBack", $"The buyer sent the parcel back ({tracking})", ct);
                if (parcel.SellerId is { } seller)
                    await _notifier.NotifyAsync(seller, NotificationKind.ReturnSentBack,
                        new Dictionary<string, string>(About(parcel.OrderId)) { ["tracking"] = tracking }, $"/shop/sales/{parcel.OrderId}", ct);
            },
            cancellationToken);

        if (!moved)
            throw new ConflictException(current.Status == ReturnStatus.Accepted
                ? $"An accepted return must be sent back within {_window.Days} days."
                : "Only an accepted return can be sent back.");

        return await ReadAsync(parcel.ShipmentId, cancellationToken);
    }

    // ---------------------------------------------------------------------------------- the seller

    public async Task<ReturnResponse> Handle(DecideSaleReturnCommand request, CancellationToken cancellationToken)
    {
        var parcel = await _returns.GetSellerParcelAsync(request.OrderId, CallerId(), cancellationToken)
            ?? throw new NotFoundException("Sale not found.");
        var current = await _returns.GetForShipmentAsync(parcel.ShipmentId, cancellationToken)
            ?? throw new NotFoundException(Returns.NoReturn);

        // A seller answers a request; an escalated one is staff's to decide.
        return await DecideAsync(parcel, current, [ReturnStatus.Requested], request.Accept, final: false, request.Reason, cancellationToken);
    }

    public async Task<ReturnResponse> Handle(ReceiveSaleReturnCommand request, CancellationToken cancellationToken)
    {
        var parcel = await _returns.GetSellerParcelAsync(request.OrderId, CallerId(), cancellationToken)
            ?? throw new NotFoundException("Sale not found.");
        return await ReceiveAsync(parcel, cancellationToken);
    }

    // ---------------------------------------------------------------------------------- staff

    public async Task<ReturnResponse> Handle(DecideReturnCommand request, CancellationToken cancellationToken)
    {
        var parcel = await _returns.GetParcelAsync(request.OrderId, request.ShipmentId, null, cancellationToken)
            ?? throw new NotFoundException(Returns.NotFound);
        var current = await _returns.GetForShipmentAsync(parcel.ShipmentId, cancellationToken)
            ?? throw new NotFoundException(Returns.NoReturn);

        if (current.Status == ReturnStatus.Escalated)
            return await DecideAsync(parcel, current, [ReturnStatus.Escalated], request.Accept, final: true, request.Reason, cancellationToken);

        // Otherwise only the shop's own parcel is staff's to answer - a seller's is theirs until escalated.
        if (parcel.SellerId is not null)
            throw new ConflictException("A seller answers a return of their own parcel; staff decide once the buyer escalates it.");

        return await DecideAsync(parcel, current, [ReturnStatus.Requested], request.Accept, final: false, request.Reason, cancellationToken);
    }

    public async Task<ReturnResponse> Handle(ReceiveReturnCommand request, CancellationToken cancellationToken)
    {
        var parcel = await _returns.GetParcelAsync(request.OrderId, request.ShipmentId, null, cancellationToken)
            ?? throw new NotFoundException(Returns.NotFound);
        if (parcel.SellerId is not null)
            throw new ConflictException("A returned parcel goes back to its seller, who marks it received.");

        return await ReceiveAsync(parcel, cancellationToken);
    }

    public async Task<ReturnPage> Handle(GetReturnsQuery request, CancellationToken cancellationToken)
    {
        ReturnStatus? status = request.Status is null ? null : Enum.Parse<ReturnStatus>(request.Status, ignoreCase: true);
        var (items, total) = await _returns.GetPageAsync(status, request.Page, request.PageSize, cancellationToken);
        return new ReturnPage(items, request.Page, request.PageSize, total);
    }

    // ---------------------------------------------------------------------------------- shared steps

    private async Task<ReturnResponse> DecideAsync(
        ReturnParcel parcel, ParcelReturn current, ReturnStatus[] from, bool accept, bool final, string? reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var to = accept ? ReturnStatus.Accepted : final ? ReturnStatus.Rejected : ReturnStatus.Refused;
        var why = accept ? null : reason?.Trim();

        var moved = await _returns.TryMoveAsync(
            new ReturnMove(current.Id, from, to, now, DecisionReason: why),
            async ct =>
            {
                await AuditAsync(current, $"Return{to}", accept ? "The return was accepted" : $"The return was {to.ToString().ToLowerInvariant()}: {why}", ct);
                await (accept
                    ? _notifier.NotifyAsync(parcel.BuyerId, NotificationKind.ReturnAccepted, About(parcel.OrderId), $"/orders/{parcel.OrderId}", ct)
                    : _notifier.NotifyAsync(parcel.BuyerId, NotificationKind.ReturnRefused,
                        new Dictionary<string, string>(About(parcel.OrderId)) { ["reason"] = why ?? string.Empty }, $"/orders/{parcel.OrderId}", ct));
            },
            cancellationToken);

        if (!moved)
            throw new ConflictException($"This return is {current.Status.ToString().ToLowerInvariant()}, and cannot be decided now.");

        return await ReadAsync(parcel.ShipmentId, cancellationToken);
    }

    private async Task<ReturnResponse> ReceiveAsync(ReturnParcel parcel, CancellationToken cancellationToken)
    {
        var current = await _returns.GetForShipmentAsync(parcel.ShipmentId, cancellationToken)
            ?? throw new NotFoundException(Returns.NoReturn);
        var now = DateTime.UtcNow;
        var amount = parcel.RefundAmount;

        var moved = await _returns.TryMoveAsync(
            new ReturnMove(current.Id, [ReturnStatus.SentBack], ReturnStatus.Received, now, RefundAmount: amount),
            async ct =>
            {
                // Money back and units back, by the services that own them - in the transaction that decided it.
                await _publish.Publish(new ParcelReturnedEvent(
                    current.Id, parcel.OrderId, parcel.ShipmentId,
                    parcel.Lines.Select(l => new ReturnedItemDto(l.SellableId, l.Quantity)).ToList(),
                    amount, parcel.Currency, now), ct);
                await AuditAsync(current, "ReturnReceived", $"The parcel came back; {amount.ToString(CultureInfo.InvariantCulture)} {parcel.Currency} to refund", ct);
                await _notifier.NotifyAsync(parcel.BuyerId, NotificationKind.ReturnRefunded,
                    new Dictionary<string, string>(About(parcel.OrderId))
                    {
                        ["amount"] = amount.ToString("0.##", CultureInfo.InvariantCulture),
                        ["currency"] = parcel.Currency,
                    }, $"/orders/{parcel.OrderId}", ct);
            },
            cancellationToken);

        if (!moved)
            throw new ConflictException($"This return is {current.Status.ToString().ToLowerInvariant()}; only a parcel on its way back can be received.");

        return await ReadAsync(parcel.ShipmentId, cancellationToken);
    }

    private async Task<(ReturnParcel Parcel, ParcelReturn Current)> MineAsync(Guid orderId, Guid shipmentId, CancellationToken cancellationToken)
    {
        var parcel = await _returns.GetParcelAsync(orderId, shipmentId, CallerId(), cancellationToken)
            ?? throw new NotFoundException(Returns.NotFound);
        var current = await _returns.GetForShipmentAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException(Returns.NoReturn);
        return (parcel, current);
    }

    private async Task<ReturnResponse> ReadAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        ReturnResponse.From(await _returns.GetForShipmentAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException(Returns.NoReturn));

    private Task AuditAsync(ParcelReturn r, string action, string summary, CancellationToken ct) =>
        _audit.RecordAsync(AuditCategory.Order, action, "Order", r.OrderId.ToString(), summary,
            after: new { Return = r.Id, Parcel = r.ShipmentId }, cancellationToken: ct);

    private static Dictionary<string, string> About(Guid orderId) => new() { ["orderId"] = orderId.ToString() };

    private Guid CallerId() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
