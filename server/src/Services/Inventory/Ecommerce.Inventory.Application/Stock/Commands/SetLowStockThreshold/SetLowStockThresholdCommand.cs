using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Application.Stock.Common;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;

namespace Ecommerce.Inventory.Application.Stock.Commands.SetLowStockThreshold;

/// <summary>
/// A variant's own low-stock line (#200, specs/102): null puts it back on the shop's default, 0 turns the notice off.
/// </summary>
public record SetLowStockThresholdCommand(Guid VariantId, int? Threshold) : IRequest<StockResponse>;

public class SetLowStockThresholdCommandValidator : AbstractValidator<SetLowStockThresholdCommand>
{
    public SetLowStockThresholdCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Threshold).InclusiveBetween(0, LowStockSettings.Max).When(x => x.Threshold is not null);
    }
}

/// <remarks>
/// The same ownership check as setting stock (specs/031), asked before the transaction and live: somebody else's
/// variant is the same 404 as a missing one.
/// </remarks>
public class SetLowStockThresholdCommandHandler(
    IUnitOfWork unitOfWork,
    IStockRepository stockRepository,
    ICurrentUser currentUser,
    IProductOwnership ownership,
    IAuditTrail audit,
    LowStockSettings lowStock) : IRequestHandler<SetLowStockThresholdCommand, StockResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IProductOwnership _ownership = ownership;
    private readonly IAuditTrail _audit = audit;
    private readonly LowStockSettings _lowStock = lowStock;

    public async Task<StockResponse> Handle(SetLowStockThresholdCommand request, CancellationToken cancellationToken)
    {
        await StockOwnership.RequireCanStockAsync(request.VariantId, _currentUser, _ownership, cancellationToken);

        StockResponse? response = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var stock = (await _stockRepository.GetForUpdateAsync([request.VariantId], ct)).FirstOrDefault()
                ?? throw new NotFoundException($"Product '{request.VariantId}' is not registered in inventory.");

            var before = new { stock.LowStockThreshold };
            stock.LowStockThreshold = request.Threshold;
            stock.UpdatedAt = DateTime.UtcNow;

            await _audit.RecordAsync(
                AuditCategory.Catalog, "LowStockThresholdSet", "Variant", stock.ProductId.ToString(),
                $"{stock.Sku} low-stock line set to {(request.Threshold is { } t ? t.ToString() : "the default")}",
                before, new { stock.LowStockThreshold }, cancellationToken: ct);
            await _stockRepository.SaveChangesAsync(ct);

            response = StockResponse.From(stock, _lowStock);
        }, cancellationToken);

        return response!;
    }
}
