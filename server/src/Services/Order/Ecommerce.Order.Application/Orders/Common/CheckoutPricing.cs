using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Shared.Exceptions;

namespace Ecommerce.Order.Application.Orders.Common;

/// <summary>
/// Works out what a checkout would cost: the caller's cart, priced by Catalog, sent to their address by
/// the option they chose, with tax for the destination (features 009-012).
/// </summary>
/// <remarks>
/// <b>Shared by the quote and the order, on purpose.</b> The storefront shows the breakdown before the
/// customer commits (issue #38). If the quote were computed anywhere else, a client or a second copy of
/// this code, it could disagree with what is then charged, and the customer would have agreed to one
/// number and paid another. Both paths call this, so the only way they can differ is if the cart, a
/// price or the address changed in between.
/// <para>
/// It stages nothing and publishes nothing. Every refusal here comes before an order exists.
/// </para>
/// </remarks>
public class CheckoutPricing(
    ICartReader cartReader,
    IAddressReader addressReader,
    IShippingOptions shippingOptions,
    ICatalogPrices catalogPrices,
    ITaxRates taxRates)
{
    private readonly ICartReader _cartReader = cartReader;
    private readonly IAddressReader _addressReader = addressReader;
    private readonly IShippingOptions _shippingOptions = shippingOptions;
    private readonly ICatalogPrices _catalogPrices = catalogPrices;
    private readonly ITaxRates _taxRates = taxRates;

    public async Task<PricedCheckout> PriceAsync(Guid? addressId, string shippingOption, CancellationToken cancellationToken)
    {
        // What is being bought comes from the caller's CART, not from the request (feature 010).
        // Read over gRPC with the caller's own token forwarded, so Cart identifies them itself.
        var cartItems = await _cartReader.GetMyCartAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            throw new ConflictException("The cart is empty, so there is nothing to order.");
        }

        // Where it goes (feature 011) - read from Identity with the caller's own token. Someone else's
        // address id comes back exactly like a missing one, so the refusal cannot confirm that it exists.
        var address = await _addressReader.GetMyAddressAsync(addressId, cancellationToken);

        if (address is null)
        {
            if (addressId is not null)
            {
                throw new NotFoundException("Delivery address not found.");
            }

            throw new ConflictException(
                "No delivery address was chosen and there is no default. Save an address first.");
        }

        // The validators have already refused an unknown code; this is the same lookup, for the price.
        var shipping = _shippingOptions.Find(shippingOption)
            ?? throw new FluentValidation.ValidationException($"'{shippingOption}' is not a delivery option.");

        // The price and the name come from Catalog, never from the request (issue #18) - and what is
        // priced is the VARIANT the customer chose (specs/020).
        var priced = await _catalogPrices.GetPricesAsync(
            cartItems.Select(i => i.SellableId).Distinct().ToList(),
            cancellationToken);

        var byVariant = priced.ToDictionary(p => p.VariantId == default ? p.ProductId : p.VariantId);

        // "Exists but cannot be sold" is an answer, not a failure, and it is the caller's to act on.
        // 409 rather than 404, so it stays distinguishable from a product that is not there.
        var unsellable = priced.Where(p => !p.Sellable).Select(p => p.Name).ToList();

        if (unsellable.Count > 0)
        {
            throw new ConflictException(
                $"Not currently for sale: {string.Join(", ", unsellable)}.");
        }

        // The total, in named parts (feature 012): goods + delivery + tax - discount. Tax follows the
        // destination, is computed per line and on delivery and rounded half away from zero (ADR-002).
        var taxRate = _taxRates.RateFor(address.Country);
        var totals = OrderTotals.Compute(
            cartItems.Select(i => (byVariant[i.SellableId].Price, i.Quantity)).ToList(), shipping.Price, taxRate);

        var lines = cartItems.Select((item, i) =>
        {
            var variant = byVariant[item.SellableId];

            return new PricedLine(
                // The product Catalog says it belongs to, not what the cart line happened to hold.
                variant.ProductId == default ? item.ProductId : variant.ProductId,
                variant.Name,
                item.Quantity,
                variant.Price,
                totals.LineTaxes[i],
                item.SellableId,
                variant.Sku,
                variant.OptionSummary);
        }).ToList();

        return new PricedCheckout(address, shipping, lines, totals, taxRate);
    }
}

/// <param name="VariantId">The sellable unit bought (specs/020).</param>
/// <param name="Sku">Frozen onto the order line: what the warehouse picks.</param>
/// <param name="OptionSummary">What the customer chose, in words. Frozen too.</param>
public record PricedLine(
    Guid ProductId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TaxAmount,
    Guid VariantId = default,
    string Sku = "",
    string OptionSummary = "")
{
    public decimal TotalPrice => UnitPrice * Quantity;
}

public record PricedCheckout(
    AddressCopy Address,
    ShippingOption Shipping,
    IReadOnlyList<PricedLine> Lines,
    OrderTotals.Result Totals,
    decimal TaxRate);
