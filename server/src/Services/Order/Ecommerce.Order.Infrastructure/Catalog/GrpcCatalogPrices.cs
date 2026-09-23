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
        IReadOnlyCollection<Guid> productIds,   // variant ids since specs/020
        CancellationToken cancellationToken = default,
        string language = "",
        string currency = "")
    {
        // PriceVariants, not GetPrices: what is bought is a variant (specs/020). GetPrices is still
        // there, unchanged, for an image built before variants.
        var request = new PriceVariantsRequest { Language = language, Currency = currency };
        request.VariantIds.AddRange(productIds.Select(id => id.ToString()));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var started = DateTime.UtcNow;

                var response = await _client.PriceVariantsAsync(
                    request,
                    deadline: DateTime.UtcNow.Add(Deadline),
                    cancellationToken: cancellationToken);

                // Printed so the real distribution stays visible as the system changes, rather than
                // being guessed at the day somebody wonders whether checkout got slower.
                _logger.LogInformation(
                    "Priced {Count} variant(s) in {Elapsed}ms.",
                    response.Variants.Count,
                    (int)(DateTime.UtcNow - started).TotalMilliseconds);

                WarnIfSellerUnknown(response.Variants);

                return response.Variants.Select(v => new CatalogPrice(
                    Guid.Parse(v.ProductId),
                    v.Name,
                    // An EMPTY price means Catalog does not sell this variant in the currency asked
                    // for (specs/022). It is carried through as null rather than parsed as zero,
                    // which would be a free camera, or defaulted to some other currency's amount,
                    // which would be the wrong money.
                    string.IsNullOrEmpty(v.Price)
                        ? null
                        // InvariantCulture both ends. A server whose locale writes "9,99" would
                        // otherwise be read as nine hundred and ninety-nine.
                        : decimal.Parse(v.Price, NumberStyles.Number, CultureInfo.InvariantCulture),
                    v.Sellable,
                    Guid.Parse(v.VariantId),
                    v.Sku,
                    v.OptionSummary,
                    v.Currency,
                    SellerOf(v),
                    SellerNameOf(v))).ToList();
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

    /// <summary>
    /// Whose product this is (specs/034): a seller, or null for the shop's own.
    /// </summary>
    /// <remarks>
    /// An UNSET field is a Catalog too old to say, and it comes back null too - the order is placed and
    /// the line records no seller, because refusing a customer's checkout over a reporting field puts
    /// the report ahead of the sale. <see cref="WarnIfSellerUnknown"/> is what keeps that from being
    /// silent.
    /// </remarks>
    public static Guid? SellerOf(PricedVariant variant) =>
        variant.HasSellerId && Guid.TryParse(variant.SellerId, out var seller) ? seller : null;

    /// <summary>
    /// The shop's name (specs/036), or null - for the shop's own goods, a name Catalog does not have,
    /// and a Catalog too old to send one. An empty string is null too: it would print as a blank.
    /// </summary>
    public static string? SellerNameOf(PricedVariant variant) =>
        variant.HasSellerName && !string.IsNullOrWhiteSpace(variant.SellerName) ? variant.SellerName : null;

    /// <summary>
    /// Says so when Catalog could not tell us whose a product is. Without this, a real seller's sale
    /// written down as nobody's would be found only when the seller asked where it went.
    /// </summary>
    private void WarnIfSellerUnknown(IEnumerable<PricedVariant> variants)
    {
        var unknown = variants.Where(v => !v.HasSellerId).Select(v => v.VariantId).ToList();

        if (unknown.Count > 0)
        {
            _logger.LogWarning(
                "Catalog did not say whose {Count} variant(s) are ({Variants}); the order lines will record "
                + "no seller, and those sales will not appear to any seller. Is Catalog older than Order?",
                unknown.Count,
                string.Join(", ", unknown));
        }
    }
}
