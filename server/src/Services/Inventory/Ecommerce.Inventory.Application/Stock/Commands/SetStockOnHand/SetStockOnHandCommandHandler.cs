using Ecommerce.Shared.Authentication;
using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Stock.Common;
using Ecommerce.Shared.Exceptions;
using MassTransit;
using MediatR;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Inventory.Application.Stock.Commands.SetStockOnHand;

/// <remarks>
/// Since specs/031 a <b>seller</b> may set the stock of a product that is theirs, and gets a 404 —
/// never a 403 — for one that is not. Before that this was administrators only, so a seller could
/// list a product, price it, and never sell it: the listing read OutOfStock forever (issue #70).
/// </remarks>
public class SetStockOnHandCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    IPublishEndpoint publishEndpoint,
    ICurrentUser currentUser,
    IProductOwnership ownership
,
    IAuditTrail audit) : IRequestHandler<SetStockOnHandCommand, StockResponse>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IProductOwnership _ownership = ownership;

    public async Task<StockResponse> Handle(SetStockOnHandCommand request, CancellationToken cancellationToken)
    {
        // BEFORE the transaction, deliberately. This is a network call, and a network call inside
        // the FOR UPDATE below would hold a row lock open for the length of a round trip to another
        // service - turning somebody else's slow Catalog into this service's blocked reservations.
        //
        // Somebody else's variant is NOT FOUND, never forbidden (specs/031). An administrator does
        // not reach Catalog at all: they pass every check anyway, and asking would make administering
        // stock fail exactly when somebody is trying to fix something.
        await StockOwnership.RequireCanStockAsync(
            request.ProductId, _currentUser, _ownership, cancellationToken);

        StockResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Same lock as the reserve path, so an adjustment cannot interleave with a reservation
            // and leave units promised against stock that no longer exists.
            var items = await _stockRepository.GetForUpdateAsync([request.ProductId], ct);

            // Reachable only AFTER ownership passed, and worded differently from the refusal
            // above on purpose: this one means "the row has not arrived from the broker yet, try
            // again", and that one means "no". Same status code, and only one is worth retrying
            // (specs/031 research D6).
            var stock = items.FirstOrDefault()
                ?? throw new NotFoundException(
                    $"Product '{request.ProductId}' is not registered in inventory.");

            if (request.QuantityOnHand < stock.QuantityReserved)
            {
                throw new ConflictException(
                    $"Cannot set quantity on hand to {request.QuantityOnHand}: "
                    + $"{stock.QuantityReserved} unit(s) are currently reserved for orders.");
            }

            var before = new { stock.QuantityOnHand, stock.QuantityReserved };
            stock.QuantityOnHand = request.QuantityOnHand;
            stock.UpdatedAt = DateTime.UtcNow;

            await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, stock, ct);

            await _audit.RecordAsync(
                AuditCategory.Catalog, "StockSet", "Variant", stock.ProductId.ToString(),
                $"{stock.Sku} stock set to {stock.QuantityOnHand}", before,
                new { stock.QuantityOnHand, stock.QuantityReserved }, cancellationToken: ct);
            await _stockRepository.SaveChangesAsync(ct);

            response = new StockResponse(
                stock.ProductId,
                stock.Sku,
                stock.QuantityOnHand,
                stock.QuantityReserved,
                stock.QuantityAvailable);
        }, cancellationToken);

        return response!;
    }
}
