using Ecommerce.Inventory.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Stock.Commands.ForgetProduct;

/// <summary>
/// Drops the stock rows for sellable units the catalogue has deleted (specs/024).
/// </summary>
/// <remarks>
/// <para>
/// The mirror of <c>RegisterProductCommand</c>. Without it the count outlives the product:
/// <c>GET /api/stock/{id}</c> keeps answering for something no catalogue has heard of, and whatever
/// was on the shelf stays there.
/// </para>
/// <para>
/// A reservation of a deleted variant has nothing to hold any more, so a <b>held</b> one is released in the
/// same transaction (#181, specs/090) - otherwise the sweeper, or the order's settlement, would act on a hold
/// whose shelf is gone. Settled ones (confirmed, released, expired) stay: they are what an order took, and
/// every settlement path already skips a stock row that is not there.
/// </para>
/// </remarks>
public record ForgetProductCommand(IReadOnlyList<Guid> ProductIds) : IRequest<int>;

public class ForgetProductCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IReservationRepository reservationRepository,
    ILogger<ForgetProductCommandHandler> logger) : IRequestHandler<ForgetProductCommand, int>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IReservationRepository _reservationRepository = reservationRepository;
    private readonly ILogger<ForgetProductCommandHandler> _logger = logger;

    /// <summary>What a hold released because its variant was deleted says it ended for (#181, specs/090).</summary>
    public const string Reason = "Product deleted";

    public async Task<int> Handle(ForgetProductCommand request, CancellationToken cancellationToken)
    {
        if (request.ProductIds.Count == 0)
        {
            return 0;
        }

        var forgotten = 0;
        var released = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // The stock rows first: deleting them waits for any reservation holding their lock, so the release
            // below - a statement that starts after - sees a hold that was being written at the same moment.
            forgotten = await _stockRepository.ForgetAsync(request.ProductIds, ct);
            released = await _reservationRepository.ReleaseHeldAsync(request.ProductIds, Reason, DateTime.UtcNow, ct);
        }, cancellationToken);

        // Zero is a normal answer, not a failure: a redelivery finds nothing left to forget, and a
        // product deleted before it was ever stocked had no row to begin with.
        _logger.LogInformation(
            "Forgot stock for {Forgotten} of {Asked} deleted sellable unit(s) and released {Released} held reservation(s).",
            forgotten, request.ProductIds.Count, released);

        return forgotten;
    }
}
