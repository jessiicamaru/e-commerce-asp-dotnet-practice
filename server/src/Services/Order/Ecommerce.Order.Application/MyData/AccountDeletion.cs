using Ecommerce.Shared.Authentication;
using MediatR;

namespace Ecommerce.Order.Application.MyData;

/// <summary>What keeps an account from being deleted (specs/112), as Identity is told it.</summary>
public static class AccountBlockers
{
    /// <summary>An order of theirs still settling, or paid with a parcel neither delivered nor cancelled.</summary>
    public const string OpenOrders = "OpenOrders";

    /// <summary>A return of theirs - or on one of their parcels - waiting for somebody to act.</summary>
    public const string OpenReturns = "OpenReturns";

    /// <summary>A seller's part of a paid order neither delivered nor cancelled.</summary>
    public const string OpenSales = "OpenSales";

    /// <summary>A seller's earnings no payout has claimed yet - on the way or due.</summary>
    public const string UnpaidEarnings = "UnpaidEarnings";
}

public interface IAccountStandingReader
{
    /// <summary>Every blocker that applies to <paramref name="personId"/>, in <see cref="AccountBlockers"/>' words.</summary>
    Task<IReadOnlyList<string>> GetBlockersAsync(Guid personId, CancellationToken cancellationToken = default);
}

public interface IAccountErasure
{
    /// <summary>
    /// Takes the person out of what Order keeps (specs/112 data-model): the delivery copies' name, street, postal code
    /// and phone, their returns' reasons; a seller's vouchers disabled and payouts without the holder's name. The
    /// orders, parcels, returns and payouts themselves stay - the books. Idempotent: fixed values, so a redelivery
    /// changes nothing.
    /// </summary>
    Task EraseAsync(Guid personId, CancellationToken cancellationToken = default);
}

/// <summary>The caller's own standing, from the token - what Order's gRPC <c>AccountStanding</c> answers.</summary>
public record GetMyStandingQuery : IRequest<IReadOnlyList<string>>;

public class GetMyStandingQueryHandler(IAccountStandingReader reader, ICurrentUser currentUser)
    : IRequestHandler<GetMyStandingQuery, IReadOnlyList<string>>
{
    private readonly IAccountStandingReader _reader = reader;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<IReadOnlyList<string>> Handle(GetMyStandingQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        return await _reader.GetBlockersAsync(me, cancellationToken);
    }
}

/// <summary>An account was deleted (<c>AccountDeleted</c>): take the person out of Order's rows.</summary>
public record EraseAccountCommand(Guid UserId) : IRequest;

public class EraseAccountCommandHandler(IAccountErasure erasure) : IRequestHandler<EraseAccountCommand>
{
    private readonly IAccountErasure _erasure = erasure;

    public Task Handle(EraseAccountCommand request, CancellationToken cancellationToken) =>
        _erasure.EraseAsync(request.UserId, cancellationToken);
}
