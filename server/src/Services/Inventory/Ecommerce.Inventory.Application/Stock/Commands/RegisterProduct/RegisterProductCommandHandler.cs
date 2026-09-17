using Ecommerce.Inventory.Application.Common;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Inventory.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Stock.Commands.RegisterProduct;

public class RegisterProductCommandHandler(
    IStockRepository stockRepository,
    IPublishEndpoint publishEndpoint,
    ILogger<RegisterProductCommandHandler> logger
) : IRequestHandler<RegisterProductCommand, bool>
{
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
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

        var stock = new StockItem
        {
            Id = Guid.CreateVersion7(),
            ProductId = request.ProductId,
            Sku = request.Sku,
            QuantityOnHand = 0,
            QuantityReserved = 0
        };

        await _stockRepository.AddAsync(stock, cancellationToken);

        // Announcing a zero is not redundant: it turns "the catalogue has never been told" into
        // "told, and the answer is no". Staged before the single save, so the stock row and the
        // announcement commit together.
        await StockAvailabilityAnnouncer.AnnounceAsync(_publishEndpoint, stock, cancellationToken);

        await _stockRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registered product {ProductId} ({Sku}) in inventory at zero units.",
            request.ProductId,
            request.Sku);

        return true;
    }
}
