using System.Globalization;
using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Contracts.Grpc;
using Grpc.Core;

namespace Ecommerce.Catalog.WebApi.Grpc;

/// <summary>
/// Answers what a product costs, for the service that has to charge for it.
/// </summary>
/// <remarks>
/// <para>
/// Catalog owns the price, so Catalog is the only thing entitled to say what it is. Before this
/// existed, Order took the unit price from the customer's own request — issue #18, where a product
/// listed at 40,000,000 was bought for 1, the stock was permanently deducted, and the payment
/// recorded <c>1.00 Approved</c>.
/// </para>
/// <para>
/// <b>One call resolves every id in an order.</b> Per-line calls would leave the caller holding
/// partial state when the third line fails, and turn "refuse the order in full" into something the
/// caller must remember rather than something the shape guarantees.
/// </para>
/// </remarks>
public class CatalogPricingService(
    IProductRepository products,
    ILogger<CatalogPricingService> logger) : CatalogPricing.CatalogPricingBase
{
    private readonly IProductRepository _products = products;
    private readonly ILogger<CatalogPricingService> _logger = logger;

    public override async Task<GetPricesResponse> GetPrices(
        GetPricesRequest request,
        ServerCallContext context)
    {
        var requested = new List<Guid>();

        foreach (var raw in request.ProductIds)
        {
            if (!Guid.TryParse(raw, out var id))
            {
                // A malformed id is the caller's mistake, not a missing product. Saying so keeps
                // NOT_FOUND meaning exactly one thing.
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument, $"'{raw}' is not a product id."));
            }

            if (!requested.Contains(id))
            {
                requested.Add(id);
            }
        }

        if (requested.Count == 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "No product ids were supplied."));
        }

        var found = await _products.GetByIdsAsync(requested, context.CancellationToken);

        // Every requested id must resolve, or the caller gets nothing. Answering for four of five
        // would let a caller build a partial order out of a successful response.
        if (found.Count != requested.Count)
        {
            var missing = requested
                .Where(id => found.All(p => p.Id != id))
                .Select(id => id.ToString());

            _logger.LogInformation(
                "Pricing request referenced unknown products: {Missing}", string.Join(", ", missing));

            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"No such product: {string.Join(", ", requested.Where(id => found.All(p => p.Id != id)))}"));
        }

        var response = new GetPricesResponse();

        foreach (var product in found)
        {
            response.Products.Add(new PricedProduct
            {
                ProductId = product.Id.ToString(),
                Name = product.Name,

                // Invariant culture, deliberately. A server whose locale writes "9,99" would send a
                // price the caller parses as nine hundred and ninety-nine.
                Price = product.Price.ToString(CultureInfo.InvariantCulture),

                // Whether it can be BOUGHT - the question being asked, rather than whichever flag
                // Catalog happens to keep. Not a failure: the caller decides what to do with it.
                Sellable = product.IsActive
            });
        }

        return response;
    }

    /// <summary>
    /// Describes products for display, answering for each one rather than refusing the lot.
    /// </summary>
    /// <remarks>
    /// Never NOT_FOUND, deliberately - that is GetPrices' job, and it is right there because a sale
    /// must be all-or-nothing. A cart being shown needs a missing product reported and the rest
    /// answered, or one deleted product would hide the price of everything else.
    /// </remarks>
    public override async Task<DescribeProductsResponse> DescribeProducts(
        DescribeProductsRequest request,
        ServerCallContext context)
    {
        var requested = new List<Guid>();
        var response = new DescribeProductsResponse();

        foreach (var raw in request.ProductIds)
        {
            if (Guid.TryParse(raw, out var id))
            {
                if (!requested.Contains(id))
                {
                    requested.Add(id);
                }
            }
            else
            {
                // Not a product id at all - it cannot exist, so it is reported as missing.
                response.MissingProductIds.Add(raw);
            }
        }

        if (requested.Count == 0)
        {
            return response;
        }

        var found = await _products.GetByIdsAsync(requested, context.CancellationToken);

        foreach (var product in found)
        {
            response.Products.Add(new PricedProduct
            {
                ProductId = product.Id.ToString(),
                Name = product.Name,
                Price = product.Price.ToString(CultureInfo.InvariantCulture),
                Sellable = product.IsActive
            });
        }

        foreach (var id in requested.Where(id => found.All(p => p.Id != id)))
        {
            response.MissingProductIds.Add(id.ToString());
        }

        return response;
    }
}
