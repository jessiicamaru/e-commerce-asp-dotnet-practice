using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
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
        RuleFor(x => x.ShippingOption).MustBeADeliveryOption(shippingOptions);
        RuleFor(x => x.VoucherCodes).MustBeVoucherCodes();
    }
}

public static class VoucherCodeRules
{
    /// <summary>At most <see cref="Vouchers.VoucherPricing.MaxCodes"/> codes, each one a code's length (specs/069).</summary>
    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> MustBeVoucherCodes<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule.Must(codes => codes is null || (codes.Count <= Vouchers.VoucherPricing.MaxCodes && codes.All(c => c is not null && c.Trim().Length <= 32)))
            .WithMessage($"At most {Vouchers.VoucherPricing.MaxCodes} voucher codes, each at most 32 characters.");
}
