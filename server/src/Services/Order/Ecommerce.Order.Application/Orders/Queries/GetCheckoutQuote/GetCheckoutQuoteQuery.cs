using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Commands.SubmitOrder;
using Ecommerce.Order.Application.Orders.Common;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetCheckoutQuote;

/// <summary>
/// What checking out would cost right now, in the same named parts the order would store, without
/// placing anything (issue #38).
/// </summary>
/// <remarks>
/// Takes exactly what <see cref="SubmitOrderCommand"/> takes, and nothing the server owns. It is a
/// quote, not a reservation: a price, the cart or the address can change before the customer commits,
/// and the order is then priced again, by the same code.
/// </remarks>
public record GetCheckoutQuoteQuery(Guid? AddressId, string ShippingOption) : IRequest<CheckoutQuoteResponse>;

public record CheckoutQuoteResponse(
    List<OrderItemResponse> Items,
    ShippingAddressResponse ShippingAddress,
    ShippingOptionResponse ShippingOption,
    decimal ShippingPrice,
    decimal Subtotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TaxRate,
    decimal TotalAmount,
    string Currency = "");

public class GetCheckoutQuoteQueryValidator : AbstractValidator<GetCheckoutQuoteQuery>
{
    public GetCheckoutQuoteQueryValidator(IShippingOptions shippingOptions)
    {
        RuleFor(x => x.ShippingOption).MustBeADeliveryOption(shippingOptions);
    }
}

public class GetCheckoutQuoteQueryHandler(CheckoutPricing pricing)
    : IRequestHandler<GetCheckoutQuoteQuery, CheckoutQuoteResponse>
{
    private readonly CheckoutPricing _pricing = pricing;

    public async Task<CheckoutQuoteResponse> Handle(GetCheckoutQuoteQuery request, CancellationToken cancellationToken)
    {
        var priced = await _pricing.PriceAsync(request.AddressId, request.ShippingOption, cancellationToken);
        var a = priced.Address;

        return new CheckoutQuoteResponse(
            priced.Lines.Select(l => new OrderItemResponse(
                l.ProductId, l.Name, l.Quantity, l.UnitPrice, l.TotalPrice, l.TaxAmount,
                l.VariantId == default ? null : l.VariantId,
                string.IsNullOrEmpty(l.Sku) ? null : l.Sku,
                string.IsNullOrEmpty(l.OptionSummary) ? null : l.OptionSummary,
                l.SellerName)).ToList(),
            new ShippingAddressResponse(a.RecipientName, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country, a.Phone),
            new ShippingOptionResponse(
                priced.Shipping.Code, priced.Shipping.Name, priced.DeliveryPrice, priced.Currency),
            priced.DeliveryPrice,
            priced.Totals.Subtotal,
            priced.Totals.Tax,
            priced.Totals.Discount,
            priced.TaxRate,
            priced.Totals.Total,
            priced.Currency);
    }
}
