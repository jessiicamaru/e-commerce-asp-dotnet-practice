using System.Text.RegularExpressions;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Application.Vouchers;

public record VoucherAmountRequest(string? Currency, decimal? FixedValue, decimal? MaxDiscount, decimal? MinSubtotal);

public record VoucherConditionRequest(string? Type, int? Value);

public record VoucherTargetRequest(string? Type, Guid Id);

/// <summary>
/// Creates a voucher (specs/069): an administrator's is the platform's, a seller's is their shop's. Whose it is
/// comes from the token, never from the body (Constitution IV) - there is no seller id to send.
/// </summary>
public record CreateVoucherCommand(
    string? Code,
    string? Name,
    string? Benefit,
    decimal? Percent,
    DateTime? StartsAt,
    DateTime? EndsAt,
    int? TotalLimit,
    int? PerCustomerLimit,
    List<VoucherAmountRequest>? Amounts,
    List<VoucherConditionRequest>? Conditions,
    List<VoucherTargetRequest>? Targets) : IRequest<VoucherSummary>;

/// <summary>The caller's vouchers: the platform's for an administrator, their own for a seller. Newest first.</summary>
public record GetMyVouchersQuery(int Page = 1, int PageSize = 12) : IRequest<PagedResponse<VoucherSummary>>;

/// <summary>Stops a voucher being used from now on. Orders that used it keep it - it is frozen on them.</summary>
public record DisableVoucherCommand(Guid Id) : IRequest<VoucherSummary>;

public static class VoucherRules
{
    public const int MaxTargets = 100;

    private static readonly Regex CodeShape = new("^[A-Z0-9][A-Z0-9-]{2,31}$", RegexOptions.Compiled);

    public static bool IsCode(string? code) => code is not null && CodeShape.IsMatch(VoucherPricing.Normalise(code));

    public static bool IsAdmin(ICurrentUser user) => user.IsInRole(StaffRoles.Admin);

    public static VoucherSummary Summary(Voucher v) => new(
        v.Id, v.Code, v.Name, v.SellerId is null, v.Benefit.ToString(), v.Percent, v.Status.ToString(), v.StartsAt, v.EndsAt,
        v.TotalLimit, v.UsedCount, v.PerCustomerLimit,
        v.Amounts.OrderBy(a => a.Currency).Select(a => new VoucherAmountResponse(a.Currency, a.FixedValue, a.MaxDiscount, a.MinSubtotal)).ToList(),
        v.Conditions.OrderBy(c => c.Type).Select(c => new VoucherConditionResponse(c.Type.ToString(), c.Value)).ToList(),
        v.Targets.Select(t => new VoucherTargetResponse(t.Type.ToString(), t.TargetId)).ToList(),
        v.CreatedAt);
}

public class CreateVoucherCommandValidator : AbstractValidator<CreateVoucherCommand>
{
    public CreateVoucherCommandValidator(IOptions<CurrencyOptions> money, ICurrentUser currentUser)
    {
        var currencies = money.Value.Supported.ToDictionary(c => c.Code, c => new Currency(c.Code, c.Decimals), StringComparer.OrdinalIgnoreCase);
        var platform = VoucherRules.IsAdmin(currentUser);

        RuleFor(x => x.Code).Must(VoucherRules.IsCode)
            .WithMessage("A code is 3-32 letters, digits or dashes, starting with a letter or digit.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Benefit).Must(b => Enum.TryParse<VoucherBenefit>(b, true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Benefit is Percent, FixedAmount or FreeShipping.");

        When(x => Is(x, VoucherBenefit.Percent), () =>
            RuleFor(x => x.Percent)
                .Must(p => p is >= 1m and <= 100m && decimal.Round(p.Value, 2) == p.Value)
                .WithMessage("A percentage is 1 to 100, with at most two decimals."));
        When(x => !Is(x, VoucherBenefit.Percent), () =>
            RuleFor(x => x.Percent).Null().WithMessage("Only a Percent voucher has a percentage."));

        // Free delivery is the platform's (research D1): the seller's delivery share stays whole.
        RuleFor(x => x.Benefit).Must(_ => platform).When(x => Is(x, VoucherBenefit.FreeShipping))
            .WithMessage("Free delivery vouchers are the platform's; a shop gives money off its goods instead.");

        RuleFor(x => x.EndsAt).Must((x, ends) => ends is null || ends > (x.StartsAt ?? DateTime.UtcNow))
            .WithMessage("A voucher must end after it starts.");
        RuleFor(x => x.TotalLimit).GreaterThanOrEqualTo(1).When(x => x.TotalLimit is not null);
        RuleFor(x => x.PerCustomerLimit).GreaterThanOrEqualTo(1).When(x => x.PerCustomerLimit is not null);
        RuleFor(x => x.PerCustomerLimit).Must((x, per) => per is null || x.TotalLimit is null || per <= x.TotalLimit)
            .WithMessage("One customer cannot be allowed more uses than the voucher has.");

        // Amounts per currency: at least one, each a supported currency once, each amount one the currency can hold.
        RuleFor(x => x.Amounts).NotEmpty().WithMessage("Say which currencies the voucher is for - it is never converted.");
        RuleFor(x => x.Amounts).Must(a => a is null || a.Select(r => r.Currency?.ToUpperInvariant()).Distinct().Count() == a.Count)
            .WithMessage("Each currency once.");
        RuleForEach(x => x.Amounts).ChildRules(amount =>
        {
            amount.RuleFor(a => a.Currency).Must(c => c is not null && currencies.ContainsKey(c))
                .WithMessage($"Currency is one of: {string.Join(", ", currencies.Keys)}.");
            amount.RuleFor(a => a).Must(a => Fits(a.FixedValue, a.Currency) && Fits(a.MaxDiscount, a.Currency) && Fits(a.MinSubtotal, a.Currency))
                .WithName("Amount").WithMessage("An amount must be one the currency can hold (dong has no decimals).");
            amount.RuleFor(a => a.MaxDiscount).GreaterThan(0m).When(a => a.MaxDiscount is not null);
            amount.RuleFor(a => a.MinSubtotal).GreaterThanOrEqualTo(0m).When(a => a.MinSubtotal is not null);
        });
        RuleForEach(x => x.Amounts).Must(a => a.FixedValue is > 0m).When(x => Is(x, VoucherBenefit.FixedAmount))
            .WithMessage("A FixedAmount voucher needs a FixedValue above 0 in every currency.");
        RuleForEach(x => x.Amounts).Must(a => a.FixedValue is null).When(x => !Is(x, VoucherBenefit.FixedAmount))
            .WithMessage("Only a FixedAmount voucher has a FixedValue.");

        RuleFor(x => x.Conditions).Must(c => c is null || c.Select(r => r.Type?.ToUpperInvariant()).Distinct().Count() == c.Count)
            .WithMessage("Each condition once.");
        RuleForEach(x => x.Conditions).ChildRules(condition =>
        {
            condition.RuleFor(c => c.Type).Must(t => Enum.TryParse<VoucherConditionType>(t, true, out var parsed) && Enum.IsDefined(parsed))
                .WithMessage("A condition is NewCustomer, FirstOrderInShop or MinQuantity.");
            condition.RuleFor(c => c.Value).NotNull().InclusiveBetween(1, 1000).When(c => IsCondition(c, VoucherConditionType.MinQuantity));
            condition.RuleFor(c => c.Value).Null().When(c => !IsCondition(c, VoucherConditionType.MinQuantity))
                .WithMessage("Only MinQuantity takes a value.");
            condition.RuleFor(c => c.Type).Must(_ => !platform).When(c => IsCondition(c, VoucherConditionType.FirstOrderInShop))
                .WithMessage("FirstOrderInShop is for a shop's own voucher; the platform's has NewCustomer.");
        });

        RuleFor(x => x.Targets).Must(t => t is null || t.Count <= VoucherRules.MaxTargets)
            .WithMessage($"At most {VoucherRules.MaxTargets} products or variants.");
        RuleForEach(x => x.Targets).ChildRules(target =>
        {
            // Category is not in part 1: Order's lines do not know their category (research D4).
            target.RuleFor(t => t.Type).Must(t => Enum.TryParse<VoucherTargetType>(t, true, out var parsed) && Enum.IsDefined(parsed))
                .WithMessage("A target is a Product or a Variant.");
            target.RuleFor(t => t.Id).NotEmpty();
        });
        RuleFor(x => x.Targets).Must(t => t is null || t.Count == 0).When(x => Is(x, VoucherBenefit.FreeShipping))
            .WithMessage("Free delivery is on the delivery, not on products.");

        bool Fits(decimal? value, string? code) =>
            value is null || code is null || !currencies.TryGetValue(code, out var currency) || (value >= 0 && currency.Fits(value.Value));
    }

    private static bool Is(CreateVoucherCommand x, VoucherBenefit benefit) =>
        Enum.TryParse<VoucherBenefit>(x.Benefit, true, out var parsed) && parsed == benefit;

    private static bool IsCondition(VoucherConditionRequest c, VoucherConditionType type) =>
        Enum.TryParse<VoucherConditionType>(c.Type, true, out var parsed) && parsed == type;
}

public class GetMyVouchersQueryValidator : AbstractValidator<GetMyVouchersQuery>
{
    public GetMyVouchersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class VoucherHandlers(IVoucherRepository vouchers, ICurrentUser currentUser, IAuditTrail audit) :
    IRequestHandler<CreateVoucherCommand, VoucherSummary>,
    IRequestHandler<GetMyVouchersQuery, PagedResponse<VoucherSummary>>,
    IRequestHandler<DisableVoucherCommand, VoucherSummary>
{
    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task<VoucherSummary> Handle(CreateVoucherCommand request, CancellationToken cancellationToken)
    {
        var caller = Caller();
        var code = VoucherPricing.Normalise(request.Code!);
        if (await _vouchers.CodeExistsAsync(code, cancellationToken))
        {
            throw new ConflictException($"The code {code} is taken.");
        }

        var now = DateTime.UtcNow;
        var id = Guid.CreateVersion7();
        var voucher = new Voucher
        {
            Id = id,
            Code = code,
            // An administrator's is the platform's; anybody else reaching here is a seller, and it is theirs.
            SellerId = VoucherRules.IsAdmin(_currentUser) ? null : caller,
            Name = request.Name!.Trim(),
            Benefit = Enum.Parse<VoucherBenefit>(request.Benefit!, true),
            Percent = request.Percent,
            StartsAt = request.StartsAt?.ToUniversalTime() ?? now,
            EndsAt = request.EndsAt?.ToUniversalTime(),
            Status = VoucherStatus.Active,
            TotalLimit = request.TotalLimit,
            PerCustomerLimit = request.PerCustomerLimit,
            CreatedBy = caller,
            CreatedAt = now,
            UpdatedAt = now,
            Amounts = request.Amounts!.Select(a => new VoucherAmount
            {
                VoucherId = id, Currency = a.Currency!.ToUpperInvariant(), FixedValue = a.FixedValue, MaxDiscount = a.MaxDiscount, MinSubtotal = a.MinSubtotal,
            }).ToList(),
            Conditions = (request.Conditions ?? []).Select(c => new VoucherCondition
            {
                VoucherId = id, Type = Enum.Parse<VoucherConditionType>(c.Type!, true), Value = c.Value,
            }).ToList(),
            Targets = (request.Targets ?? []).DistinctBy(t => (t.Type!.ToUpperInvariant(), t.Id)).Select(t => new VoucherTarget
            {
                VoucherId = id, Type = Enum.Parse<VoucherTargetType>(t.Type!, true), TargetId = t.Id,
            }).ToList(),
        };

        await _vouchers.AddAsync(voucher, cancellationToken);
        var summary = VoucherRules.Summary(voucher);
        await _audit.RecordAsync(
            AuditCategory.Order, "VoucherCreated", "Voucher", id.ToString(),
            $"Voucher {code} created ({(voucher.SellerId is null ? "platform" : "shop")}, {voucher.Benefit})",
            after: summary, cancellationToken: cancellationToken);

        if (!await _vouchers.TrySaveNewAsync(cancellationToken))
        {
            throw new ConflictException($"The code {code} is taken.");   // two creators at once: the unique index decides
        }

        return summary;
    }

    public async Task<PagedResponse<VoucherSummary>> Handle(GetMyVouchersQuery request, CancellationToken cancellationToken)
    {
        var caller = Caller();
        var (items, total) = await _vouchers.GetPageAsync(VoucherRules.IsAdmin(_currentUser) ? null : caller, request.Page, request.PageSize, cancellationToken);
        return new PagedResponse<VoucherSummary>(items, request.Page, request.PageSize, total);
    }

    public async Task<VoucherSummary> Handle(DisableVoucherCommand request, CancellationToken cancellationToken)
    {
        var caller = Caller();
        var staff = VoucherRules.IsAdmin(_currentUser);
        var (outcome, voucher) = await _vouchers.TryDisableAsync(request.Id, caller, staff, DateTime.UtcNow,
            (v, ct) => _audit.RecordAsync(
                AuditCategory.Order, "VoucherDisabled", "Voucher", v.Id.ToString(), $"Voucher {v.Code} disabled",
                new { Status = "Active" }, new { Status = "Disabled" }, cancellationToken: ct),
            cancellationToken);

        return outcome switch
        {
            DisableOutcome.NotFound => throw new NotFoundException("Voucher not found."),
            DisableOutcome.AlreadyDisabled => throw new ConflictException("This voucher is already disabled."),
            _ => VoucherRules.Summary(voucher!),
        };
    }

    private Guid Caller() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
