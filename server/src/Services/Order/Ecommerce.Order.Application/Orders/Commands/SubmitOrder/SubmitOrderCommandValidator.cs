using Ecommerce.Order.Application.Common.Interfaces;
using FluentValidation;

namespace Ecommerce.Order.Application.Orders.Commands.SubmitOrder;

/// <remarks>
/// The delivery option is checked here, against the options Order actually offers, so an unknown one is
/// a 400 before anything is read from another service. An empty cart and a missing address are refused
/// by the handler, because only the handler has asked.
/// </remarks>
public class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
{
    public SubmitOrderCommandValidator(IShippingOptions shippingOptions)
    {
        RuleFor(x => x.ShippingOption)
            .NotEmpty()
            .WithMessage("Choose a delivery option.")
            .Must(code => shippingOptions.Find(code) is not null)
            .WithMessage(x => $"'{x.ShippingOption}' is not a delivery option. Offered: "
                + string.Join(", ", shippingOptions.All.Select(o => o.Code)) + ".");
    }
}
