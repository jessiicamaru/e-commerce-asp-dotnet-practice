using System.Globalization;
using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Contracts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Cart.Infrastructure.Catalog;

/// <summary>
/// Asks Catalog what the products in a cart look like right now, for display.
/// </summary>
/// <remarks>
/// Calls DescribeProducts, never GetPrices. GetPrices refuses the whole request if one product is
/// missing - right for deciding a sale, wrong for showing a cart, where one deleted product would hide
/// the price of everything else.
/// </remarks>
public class GrpcCatalogProducts(
    CatalogPricing.CatalogPricingClient client,
    ILogger<GrpcCatalogProducts> logger) : ICatalogProducts
{
    private readonly CatalogPricing.CatalogPricingClient _client = client;
    private readonly ILogger<GrpcCatalogProducts> _logger = logger;

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    public async Task<CatalogDescription> DescribeAsync(
        IReadOnlyCollection<Guid> productIds,   // sellable ids: variants (specs/020)
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return new CatalogDescription(true, [], []);
        }

        var request = new DescribeVariantsRequest();
        request.VariantIds.AddRange(productIds.Select(id => id.ToString()));

        try
        {
            var response = await _client.DescribeVariantsAsync(
                request,
                deadline: DateTime.UtcNow.Add(Deadline),
                cancellationToken: cancellationToken);

            return new CatalogDescription(
                true,
                response.Variants.Select(v => new CatalogProduct(
                    Guid.Parse(v.ProductId),
                    v.Name,
                    decimal.Parse(v.Price, NumberStyles.Number, CultureInfo.InvariantCulture),
                    v.Sellable,
                    Guid.Parse(v.VariantId),
                    v.OptionSummary)).ToList(),
                response.MissingVariantIds.Select(Guid.Parse).ToList());
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            // Showing a cart without prices beats refusing to show it at all: what the customer chose
            // is the cart's own data. Checkout still refuses with 503, because there the price is not
            // optional - that decision is not made here.
            _logger.LogWarning(ex, "Catalog unreachable while reading a cart; showing it without prices.");
            return CatalogDescription.Unreachable();
        }
    }
}
