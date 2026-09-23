using Ecommerce.Contracts.Grpc;
using Ecommerce.Inventory.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Inventory.Infrastructure.Catalog;

/// <summary>
/// Asks Catalog who a variant belongs to, over gRPC (specs/031).
/// </summary>
/// <remarks>
/// <para>
/// <b>Inventory's first synchronous dependency on another service.</b> Everything it did before
/// this was messages. The cost is real and was accepted rather than overlooked: Catalog
/// unreachable now means a seller cannot set stock. That is tolerable because Catalog unreachable
/// also means nobody can see the product — so nothing is lost by also being unable to stock it.
/// </para>
/// <para>
/// h2c on a port of its own, like every gRPC edge in this system: one plaintext port cannot carry
/// HTTP/1.1 and HTTP/2, because telling them apart needs ALPN and ALPN is part of TLS.
/// </para>
/// <para>
/// A failure is a <b>503</b>, never a 404. "I could not find out whether this is yours" and "this
/// is not yours" are different answers, and collapsing them would tell a seller she does not own
/// her own shop whenever Catalog hiccuped.
/// </para>
/// </remarks>
public class GrpcProductOwnership(
    CatalogOwnership.CatalogOwnershipClient client,
    ILogger<GrpcProductOwnership> logger) : IProductOwnership
{
    private readonly CatalogOwnership.CatalogOwnershipClient _client = client;
    private readonly ILogger<GrpcProductOwnership> _logger = logger;

    // Bounded, like Order's client and for the same reason: a slow Catalog must become a refusal
    // rather than a request that never returns. Setting stock is a person waiting on a form.
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1)];

    public async Task<VariantOwnership?> GetAsync(
        Guid variantId, CancellationToken cancellationToken = default)
    {
        var request = new GetVariantOwnersRequest();
        request.VariantIds.Add(variantId.ToString());

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _client.GetVariantOwnersAsync(
                    request,
                    deadline: DateTime.UtcNow.Add(Deadline),
                    cancellationToken: cancellationToken);

                var owner = response.Owners.FirstOrDefault();
                if (owner is null)
                {
                    // Absent means no such variant. The caller turns that into its own 404, worded
                    // exactly like "not yours" - which is the point (specs/027).
                    return null;
                }

                Guid? sellerId = Guid.TryParse(owner.SellerId, out var parsed) ? parsed : null;
                return new VariantOwnership(variantId, Guid.Parse(owner.ProductId), sellerId);
            }
            catch (RpcException ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                _logger.LogWarning(
                    ex, "Catalog ownership lookup failed ({Status}), attempt {Attempt} of {Max}.",
                    ex.StatusCode, attempt, MaxAttempts);
                await Task.Delay(Backoff[attempt - 1], cancellationToken);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Catalog could not be asked who owns variant {VariantId}.", variantId);
                throw new DependencyUnavailableException(
                    "The catalogue could not be reached, so it is not possible to tell whether this "
                    + "product is yours. Try again in a moment.");
            }
        }
    }

    private static bool IsTransient(RpcException ex) =>
        ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.Internal;
}
