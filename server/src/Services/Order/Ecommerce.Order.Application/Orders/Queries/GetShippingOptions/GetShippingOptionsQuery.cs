using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Money;
using MediatR;

namespace Ecommerce.Order.Application.Orders.Queries.GetShippingOptions;

public record GetShippingOptionsQuery : IRequest<IReadOnlyList<ShippingOptionResponse>>;

/// <summary>
/// The delivery options a customer can actually choose, with what each costs in the currency they are
/// shopping in (specs/022).
/// </summary>
/// <remarks>
/// An option with no price in that currency is <b>not offered</b>. Listing it would either show an
/// amount in the wrong currency or show none at all, and both are worse than a shorter list.
/// </remarks>
public class GetShippingOptionsQueryHandler(IShippingOptions options, IRequestCurrency currency)
    : IRequestHandler<GetShippingOptionsQuery, IReadOnlyList<ShippingOptionResponse>>
{
    private readonly IShippingOptions _options = options;
    private readonly IRequestCurrency _currency = currency;

    public Task<IReadOnlyList<ShippingOptionResponse>> Handle(
        GetShippingOptionsQuery request, CancellationToken cancellationToken)
    {
        var asked = _currency.Current;

        IReadOnlyList<ShippingOptionResponse> offered = _options.Offered(asked.Code)
            .Select(option => new ShippingOptionResponse(
                option.Code, option.Name, option.PriceIn(asked.Code), asked.Code))
            .ToList();

        return Task.FromResult(offered);
    }
}
