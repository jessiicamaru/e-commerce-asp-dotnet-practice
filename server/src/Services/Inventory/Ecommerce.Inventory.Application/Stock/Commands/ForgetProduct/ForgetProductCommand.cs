using Ecommerce.Inventory.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Application.Stock.Commands.ForgetProduct;

/// <summary>
/// Drops the stock rows for sellable units the catalogue has deleted (specs/024).
/// </summary>
/// <remarks>
/// The mirror of <c>RegisterProductCommand</c>. Without it the count outlives the product:
/// <c>GET /api/stock/{id}</c> keeps answering for something no catalogue has heard of, and whatever
/// was on the shelf stays there.
/// </remarks>
public record ForgetProductCommand(IReadOnlyList<Guid> ProductIds) : IRequest<int>;

public class ForgetProductCommandHandler(
    IStockRepository stockRepository,
    ILogger<ForgetProductCommandHandler> logger) : IRequestHandler<ForgetProductCommand, int>
{
    private readonly IStockRepository _stockRepository = stockRepository;
    private readonly ILogger<ForgetProductCommandHandler> _logger = logger;

    public async Task<int> Handle(ForgetProductCommand request, CancellationToken cancellationToken)
    {
        if (request.ProductIds.Count == 0)
        {
            return 0;
        }

        var forgotten = await _stockRepository.ForgetAsync(request.ProductIds, cancellationToken);

        // Zero is a normal answer, not a failure: a redelivery finds nothing left to forget, and a
        // product deleted before it was ever stocked had no row to begin with.
        _logger.LogInformation(
            "Forgot stock for {Forgotten} of {Asked} deleted sellable unit(s).",
            forgotten, request.ProductIds.Count);

        return forgotten;
    }
}
