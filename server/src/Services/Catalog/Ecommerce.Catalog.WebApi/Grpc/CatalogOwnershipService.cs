using Ecommerce.Catalog.Application.Common.Interfaces;
using Ecommerce.Contracts.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Catalog.WebApi.Grpc;

/// <summary>
/// Answers who a listing belongs to, for the service that has to refuse somebody (specs/031).
/// </summary>
/// <remarks>
/// <para>
/// Ownership lives in <c>products.SellerId</c>, a Catalog column, and Inventory holds only variant
/// ids — so "may this seller stock this?" is two hops that both land here. Before this existed a
/// seller could list a product, price it, and then not stock it: <c>PUT /api/stock/{id}</c> on her
/// own product answered <b>403</b> (issue #70).
/// </para>
/// <para>
/// <b>Answers, never decides.</b> This reports a fact; Inventory compares it to its own caller and
/// refuses. A method here called <c>MayStock</c> would move an authorization rule into the service
/// that does not hold the row being written, and would have to be told who is asking — which is the
/// shape of the defect specs/009 and specs/027 both fixed.
/// </para>
/// <para>
/// <b>A variant that does not exist is simply absent.</b> Not an error, not a null entry: the
/// caller turns absence into its own 404, which is what keeps "not yours" and "no such variant"
/// worded identically at the edge.
/// </para>
/// </remarks>
// Service to service on the h2c port, which the gateway does not route: Inventory asks before it lets a
// seller stock, and sends no token. Anonymous as it always was, but now by saying so - the fallback policy
// refuses anything that does not (specs/089).
[AllowAnonymous]
public class CatalogOwnershipService(
    IProductRepository products,
    ILogger<CatalogOwnershipService> logger) : CatalogOwnership.CatalogOwnershipBase
{
    private readonly IProductRepository _products = products;
    private readonly ILogger<CatalogOwnershipService> _logger = logger;

    public override async Task<GetVariantOwnersResponse> GetVariantOwners(
        GetVariantOwnersRequest request,
        ServerCallContext context)
    {
        var wanted = new List<Guid>();

        foreach (var raw in request.VariantIds)
        {
            // An unparseable id is dropped rather than refused: it cannot name a real variant, so
            // it belongs in the same bucket as one that does not exist.
            if (Guid.TryParse(raw, out var id))
            {
                wanted.Add(id);
            }
        }

        if (wanted.Count == 0)
        {
            return new GetVariantOwnersResponse();
        }

        var owners = await _products.GetVariantOwnersAsync(wanted, context.CancellationToken);

        var response = new GetVariantOwnersResponse();
        foreach (var owner in owners)
        {
            response.Owners.Add(new VariantOwner
            {
                VariantId = owner.VariantId.ToString(),
                ProductId = owner.ProductId.ToString(),
                // Empty string means the shop itself. proto3 has no null for a string, and an
                // absent seller is a real state (specs/027), not a missing one.
                SellerId = owner.SellerId?.ToString() ?? string.Empty,
            });
        }

        if (owners.Count != wanted.Count)
        {
            _logger.LogDebug(
                "Asked about {Wanted} variant(s), {Found} exist. The caller decides what absence means.",
                wanted.Count, owners.Count);
        }

        return response;
    }
}
