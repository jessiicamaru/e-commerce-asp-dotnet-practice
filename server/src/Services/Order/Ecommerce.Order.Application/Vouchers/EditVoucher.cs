using System.Text.Json.Serialization;
using Ecommerce.Order.Domain.Enums;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Options;
using ValidationException = FluentValidation.ValidationException;

namespace Ecommerce.Order.Application.Vouchers;

/// <summary>A currency's new minimum subtotal (specs/113); null is "no minimum".</summary>
public record VoucherMinSubtotalRequest(string? Currency, decimal? MinSubtotal);

/// <summary>
/// Corrects an active voucher's terms (specs/113, #219): what decides WHETHER it applies - name, end, limits, minimums -
/// never how much it takes off. Every field is the new value; there is no field for the benefit, the amounts, the
/// targets, the code or the start, so they cannot change.
/// </summary>
public record EditVoucherCommand(
    string? Name,
    DateTime? EndsAt,
    int? TotalLimit,
    int? PerCustomerLimit,
    List<VoucherMinSubtotalRequest>? MinSubtotals,
    int? MinQuantity) : IRequest<VoucherSummary>
{
    /// <summary>From the route, never the body.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}

/// <summary>What an edit writes, once checked.</summary>
public record VoucherEdit(
    string Name, DateTime? EndsAt, int? TotalLimit, int? PerCustomerLimit, IReadOnlyDictionary<string, decimal?> MinSubtotals, int? MinQuantity);

public enum EditOutcome { NotFound, Disabled, BelowUses, Edited }

public class EditVoucherCommandValidator : AbstractValidator<EditVoucherCommand>
{
    public EditVoucherCommandValidator(IOptions<CurrencyOptions> money)
    {
        var currencies = money.Value.Supported.ToDictionary(c => c.Code, c => new Currency(c.Code, c.Decimals), StringComparer.OrdinalIgnoreCase);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EndsAt).Must(ends => ends is null || ends.Value.ToUniversalTime() > DateTime.UtcNow)
            .WithMessage("The end must be in the future. To stop a voucher now, disable it.");
        RuleFor(x => x.TotalLimit).GreaterThanOrEqualTo(1).When(x => x.TotalLimit is not null);
        RuleFor(x => x.PerCustomerLimit).GreaterThanOrEqualTo(1).When(x => x.PerCustomerLimit is not null);
        RuleFor(x => x.PerCustomerLimit).Must((x, per) => per is null || x.TotalLimit is null || per <= x.TotalLimit)
            .WithMessage("A customer cannot be allowed more uses than the voucher has.");
        RuleFor(x => x.MinQuantity).InclusiveBetween(1, 1000).When(x => x.MinQuantity is not null);
        RuleFor(x => x.MinSubtotals).Must(m => m is null || m.Select(r => r.Currency?.ToUpperInvariant()).Distinct().Count() == m.Count)
            .WithMessage("Name each currency once.");
        RuleForEach(x => x.MinSubtotals).ChildRules(row =>
        {
            row.RuleFor(r => r.Currency).Must(c => c is not null && currencies.ContainsKey(c)).WithMessage("The shop does not sell in that currency.");
            row.RuleFor(r => r.MinSubtotal).GreaterThanOrEqualTo(0m).When(r => r.MinSubtotal is not null);
            row.RuleFor(r => r).Must(r => r.MinSubtotal is null || r.Currency is null || !currencies.TryGetValue(r.Currency, out var currency) || currency.Fits(r.MinSubtotal.Value))
                .WithMessage("That amount is finer than the currency's smallest unit.");
        });
    }
}

public class EditVoucherCommandHandler(IVoucherRepository vouchers, ICurrentUser currentUser, IAuditTrail audit)
    : IRequestHandler<EditVoucherCommand, VoucherSummary>
{
    public const string BelowUses = "The total limit cannot be lower than the uses already made.";

    private readonly IVoucherRepository _vouchers = vouchers;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task<VoucherSummary> Handle(EditVoucherCommand request, CancellationToken cancellationToken)
    {
        var caller = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        // An administrator edits the platform's vouchers; a seller their own (research D3). Any other is not there.
        var owner = VoucherRules.IsAdmin(_currentUser) ? (Guid?)null : caller;

        var current = await _vouchers.GetAsync(request.Id, cancellationToken);
        if (current is null || current.SellerId != owner)
        {
            throw new NotFoundException("Voucher not found.");
        }

        if (current.Status == VoucherStatus.Disabled)
        {
            throw new ConflictException("This voucher is disabled and is not edited.");
        }

        var failures = new List<ValidationFailure>();
        var endsAt = request.EndsAt?.ToUniversalTime();
        if (endsAt is not null && endsAt <= current.StartsAt)
        {
            failures.Add(new ValidationFailure(nameof(request.EndsAt), "The end must be after the start."));
        }

        var theirs = current.Amounts.Select(a => a.Currency).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in request.MinSubtotals ?? [])
        {
            if (!theirs.Contains(row.Currency!))
            {
                failures.Add(new ValidationFailure(nameof(request.MinSubtotals),
                    $"This voucher has no amount in {row.Currency!.ToUpperInvariant()}; an edit does not add currencies."));
            }
        }

        if (request.MinQuantity is not null && current.Conditions.All(c => c.Type != VoucherConditionType.MinQuantity))
        {
            failures.Add(new ValidationFailure(nameof(request.MinQuantity), "This voucher has no minimum quantity to change."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        if (request.TotalLimit is { } total && total < current.UsedCount)
        {
            throw new ConflictException(BelowUses);
        }

        var edit = new VoucherEdit(
            request.Name!.Trim(), endsAt, request.TotalLimit, request.PerCustomerLimit,
            (request.MinSubtotals ?? []).ToDictionary(r => r.Currency!.ToUpperInvariant(), r => r.MinSubtotal),
            request.MinQuantity);
        var before = VoucherRules.Summary(current);

        var (outcome, edited) = await _vouchers.TryEditAsync(request.Id, owner, edit, DateTime.UtcNow,
            (after, ct) => _audit.RecordAsync(
                AuditCategory.Order, "VoucherEdited", "Voucher", after.Id.ToString(), $"Voucher {after.Code} edited",
                before, after, cancellationToken: ct),
            cancellationToken);

        return outcome switch
        {
            EditOutcome.NotFound => throw new NotFoundException("Voucher not found."),
            EditOutcome.Disabled => throw new ConflictException("This voucher is disabled and is not edited."),
            EditOutcome.BelowUses => throw new ConflictException(BelowUses),
            _ => edited!,
        };
    }
}
