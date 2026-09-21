using System.Globalization;
using Ecommerce.Contracts.Grpc;
using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Infrastructure.Catalog;

/// <summary>
/// Asks Catalog what things cost, over gRPC.
/// </summary>
/// <remarks>
/// <para>
/// The transport lives here and nowhere else. <c>ICatalogPrices</c> is declared in the Application
/// layer, which depends on abstractions only and never on a transport package — so replacing gRPC
/// with REST is this one file.
/// </para>
/// <para>
/// This is the <b>first synchronous cross-service call in the system</b>. Everything else is
/// messages. The consequence is real and deliberate: Catalog being unreachable now stops orders
/// being placed, where before they were placed at whatever price the customer claimed.
/// </para>
/// </remarks>
public class GrpcCatalogPrices(
    CatalogPricing.CatalogPricingClient client,
    ILogger<GrpcCatalogPrices> logger) : ICatalogPrices
{
    private readonly CatalogPricing.CatalogPricingClient _client = client;
    private readonly ILogger<GrpcCatalogPrices> _logger = logger;

    // Bounded, and the bound matters as much as the retry. Catalog now sits on checkout's critical
    // path, so a slow Catalog is a slow checkout for everybody and the wait has to end. Retrying
    // forever would turn an outage into requests that never return, which is worse than a refusal
    // because nothing reports it.
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1)];

    public async Task<IReadOnlyList<CatalogPrice>> GetPricesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPricesRequest();
        request.ProductIds.AddRange(productIds.Select(id => id.ToString()));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var started = DateTime.UtcNow;

                var response = await _client.GetPricesAsync(
                    request,
                    deadline: DateTime.UtcNow.Add(Deadline),
                    cancellationToken: cancellationToken);

                // Printed so the real distribution stays visible as the system changes, rather than
                // being guessed at the day somebody wonders whether checkout got slower.
                _logger.LogInformation(
                    "Priced {Count} product(s) in {Elapsed}ms.",
                    response.Products.Count,
                    (int)(DateTime.UtcNow - started).TotalMilliseconds);

                return response.Products.Select(p => new CatalogPrice(
                    Guid.Parse(p.ProductId),
                    p.Name,
                    // InvariantCulture both ends. A server whose locale writes "9,99" would
                    // otherwise be read as nine hundred and ninety-nine.
                    decimal.Parse(p.Price, NumberStyles.Number, CultureInfo.InvariantCulture),
                    p.Sellable)).ToList();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                // NOT a retry case, and NOT the same as being unable to ask. Retrying a NOT_FOUND
                // is three times the latency for the same refusal.
                throw new NotFoundException(ex.Status.Detail);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                throw new NotFoundException(ex.Status.Detail);
            }
            catch (RpcException ex) when (
                ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
            {
                if (attempt >= MaxAttempts)
                {
                    _logger.LogWarning(
                        ex, "Catalog did not answer after {Attempts} attempts.", MaxAttempts);

                    // 503, not 500, and not 404. A 500 says "we are broken"; this says "a
                    // dependency is down, try again shortly". Reporting it as 404 would send a
                    // customer to check a catalogue that is merely offline.
                    throw new DependencyUnavailableException(
                        $"The catalogue could not be reached after {MaxAttempts} attempts, so the "
                        + "order was not placed. Nothing was charged and no stock was reserved.");
                }

                var wait = Backoff[attempt - 1];

                _logger.LogInformation(
                    "Catalog unreachable ({Status}); attempt {Attempt}/{Max}, retrying in {Wait}ms.",
                    ex.StatusCode, attempt, MaxAttempts, (int)wait.TotalMilliseconds);

                await Task.Delay(wait, cancellationToken);
            }
        }
    }
}
