using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;

public class RegisterProductCommandHandler(
    IStockRepository stockRepository,
    ILogger<RegisterProductCommandHandler> logger
) : IRequestHandler<RegisterProductCommand, bool>
{
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly ILogger<RegisterProductCommandHandler> _logger = logger;

    public async Task<bool> Handle(RegisterProductCommand request, CancellationToken cancellationToken)
    {
        // ProductCreatedEvent redelivers like any other message; a second delivery must not reset
        // a product that has since been stocked.
        var existing = await _stockRepository.GetByProductIdAsync(request.ProductId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Product {ProductId} is already registered in inventory; leaving it untouched.",
                request.ProductId);

            return false;
        }

        await _stockRepository.AddAsync(new StockItem
        {
            Id = Guid.CreateVersion7(),
            ProductId = request.ProductId,
            Sku = request.Sku,
            QuantityOnHand = 0,
            QuantityReserved = 0
        }, cancellationToken);

        await _stockRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registered product {ProductId} ({Sku}) in inventory at zero units.",
            request.ProductId,
            request.Sku);

        return true;
    }
}
