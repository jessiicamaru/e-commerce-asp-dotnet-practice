using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using CartItem = Ecommerce.Order.Application.Common.Interfaces.CartItem;

namespace Ecommerce.Order.Infrastructure.Cart;

/// <summary>
/// Reads the caller's cart from the Cart service, forwarding the caller's own token to do it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Token propagation</b> is the whole point of this class. The request message is empty; Cart
/// learns WHOSE cart from the Authorization header copied here from the incoming request. Cart then
/// validates that token itself - it does not trust Order to say who the customer is.
/// </para>
/// <para>
/// The second synchronous dependency checkout has, after Catalog. Cart being unreachable now refuses
/// orders with 503; that cost is recorded in specs/010-customer-cart research D4.
/// </para>
/// </remarks>
public class GrpcCartReader(
    CartReading.CartReadingClient client,
    IHttpContextAccessor httpContextAccessor,
    ILogger<GrpcCartReader> logger) : ICartReader
{
    private readonly CartReading.CartReadingClient _client = client;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<GrpcCartReader> _logger = logger;

    private const int MaxAttempts = 3;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1)];

    public async Task<IReadOnlyList<CartItem>> GetMyCartAsync(CancellationToken cancellationToken = default)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization))
        {
            // Checkout is [Authorize], so this is a wiring fault, not a customer's mistake.
            throw new UnauthorizedAccessException("No token to forward to the Cart service.");
        }

        var headers = new Metadata { { "Authorization", authorization } };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await _client.GetMyCartAsync(
                    new GetMyCartRequest(),
                    headers,
                    deadline: DateTime.UtcNow.Add(Deadline),
                    cancellationToken: cancellationToken);

                return response.Items
                    .Select(i => new CartItem(
                        Guid.Parse(i.ProductId),
                        i.Quantity,
                        // Empty from a Cart built before variants; CartItem.SellableId falls back.
                        string.IsNullOrEmpty(i.VariantId) ? default : Guid.Parse(i.VariantId)))
                    .ToList();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
            {
                // Cart rejected the forwarded token. Not retried: it will reject it again.
                throw new UnauthorizedAccessException("The Cart service did not accept the forwarded token.");
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                if (attempt >= MaxAttempts)
                {
                    _logger.LogWarning(ex, "Cart did not answer after {Attempts} attempts.", MaxAttempts);

                    throw new DependencyUnavailableException(
                        $"The cart could not be read after {MaxAttempts} attempts, so the order was not "
                        + "placed. Nothing was charged and no stock was reserved.");
                }

                await Task.Delay(Backoff[attempt - 1], cancellationToken);
            }
        }
    }
}
