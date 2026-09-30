using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;

namespace Ecommerce.Order.Application.Vouchers;

/// <summary>
/// A voucher as a shopper sees it listed (specs/114, #220): what it gives and until when, in the request's currency -
/// never how many are left, which is the owner's business (research D3).
/// </summary>
public record PublicVoucherResponse(
    string Code,
    string Name,
    bool IsPlatform,
    Guid? SellerId,
    string Benefit,
    decimal? Percent,
    string Currency,
    decimal? FixedValue,
    decimal? MaxDiscount,
    decimal? MinSubtotal,
    DateTime? EndsAt,
    List<VoucherConditionResponse> Conditions,
    bool Targeted);

/// <summary>
/// The public vouchers that could apply (specs/114): the platform's if asked, and each named shop's; with a product,
/// only those for everything or naming it or one of its variants. Anonymous - showing one is not a secret.
/// </summary>
public record GetPublicVouchersQuery(bool Platform, List<Guid>? SellerIds, Guid? ProductId, List<Guid>? VariantIds)
    : IRequest<List<PublicVoucherResponse>>;

/// <summary>What the list is asked for, once checked: the scope and the currency.</summary>
public record PublicVoucherScope(bool Platform, IReadOnlyCollection<Guid> SellerIds, Guid? ProductId, IReadOnlyCollection<Guid> VariantIds, string Currency);

public static class PublicVouchers
{
    /// <summary>The shop's one page size: a shop with more live vouchers than this is not a case it has (research D5).</summary>
    public const int Limit = 12;
}

public class GetPublicVouchersQueryValidator : AbstractValidator<GetPublicVouchersQuery>
{
    public GetPublicVouchersQueryValidator()
    {
        RuleFor(x => x).Must(x => x.Platform || x.SellerIds is { Count: > 0 })
            .WithName("scope").WithMessage("Ask for the platform's vouchers, a shop's, or both.");
        RuleFor(x => x.SellerIds).Must(s => s is null || s.Count <= 20).WithMessage("At most 20 shops at once.");
        RuleFor(x => x.VariantIds).Must(v => v is null || v.Count <= 50).WithMessage("At most 50 variants at once.");
        RuleFor(x => x.VariantIds).Must(v => v is null || v.Count == 0).When(x => x.ProductId is null)
            .WithMessage("Variants narrow a product; name the product too.");
    }
}

public class GetPublicVouchersQueryHandler(IVoucherRepository vouchers, IRequestCurrency currency)
    : IRequestHandler<GetPublicVouchersQuery, List<PublicVoucherResponse>>
{
    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly IRequestCurrency _currency = currency;

    public Task<List<PublicVoucherResponse>> Handle(GetPublicVouchersQuery request, CancellationToken cancellationToken) =>
        _vouchers.GetPublicAsync(
            new PublicVoucherScope(
                request.Platform,
                (request.SellerIds ?? []).Distinct().ToList(),
                request.ProductId,
                (request.VariantIds ?? []).Distinct().ToList(),
                _currency.Current.Code),
            DateTime.UtcNow,
            cancellationToken);
}
