using Ecommerce.Order.Application.Common.Interfaces;
using FluentValidation;

namespace Ecommerce.Order.Application.Orders.Common;

public static class DeliveryOptionRule
{
    /// <summary>
    /// A delivery option Order actually offers, so an unknown one is a 400 before anything is read from
    /// another service. Shared by checkout and its quote, which must refuse the same things.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeADeliveryOption<T>(
        this IRuleBuilder<T, string> rule, IShippingOptions shippingOptions) =>
        rule
            .NotEmpty()
            .WithMessage("Choose a delivery option.")
            .Must(code => shippingOptions.Find(code) is not null)
            .WithMessage((_, code) => $"'{code}' is not a delivery option. Offered: "
                + string.Join(", ", shippingOptions.All.Select(o => o.Code)) + ".");
}
