using System.Text.RegularExpressions;
using Ecommerce.Order.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Application.Delivery;

/// <summary>
/// Where delivery options and the carrier are stored (#196, specs/098). Checkout reads them through
/// <see cref="Common.Interfaces.IShippingOptions"/>; administrators edit them through this.
/// </summary>
public interface IDeliveryRepository
{
    /// <summary>Every option, offered or not, with its prices, in the shoppers' order.</summary>
    Task<List<DeliveryOption>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<DeliveryOption?> FindOptionAsync(string code, CancellationToken cancellationToken = default);

    void AddOption(DeliveryOption option);

    /// <summary>How many options are offered, not counting <paramref name="except"/>.</summary>
    Task<int> CountOfferedAsync(string except, CancellationToken cancellationToken = default);

    Task<Carrier?> GetCarrierAsync(CancellationToken cancellationToken = default);

    void AddCarrier(Carrier carrier);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public record DeliveryOptionResponse(
    string Code, string Name, bool IsActive, int SortOrder, IReadOnlyDictionary<string, decimal> Prices, int? MinDays = null, int? MaxDays = null)
{
    public static DeliveryOptionResponse From(DeliveryOption option) => new(
        option.Code, option.Name, option.IsActive, option.SortOrder,
        option.Prices.OrderBy(p => p.Currency).ToDictionary(p => p.Currency, p => p.Amount), option.MinDays, option.MaxDays);
}

/// <summary>
/// What a delivery time may be (specs/134): both ends or neither, whole business days from 0 to <see cref="LongestDays"/>,
/// the soonest not after the latest. One rule for the editor and for configuration; the table's CHECK says it again.
/// </summary>
public static class DeliveryEstimate
{
    public const int LongestDays = 60;

    /// <summary>Why this is not a delivery time, or null when it is one.</summary>
    public static string? Problem(int? minDays, int? maxDays) => (minDays, maxDays) switch
    {
        (null, null) => null,
        (null, _) or (_, null) => "A delivery time needs both the soonest and the latest day, or neither.",
        ( < 0, _) => "The soonest day cannot be negative.",
        (_, > LongestDays) => $"The latest day can be at most {LongestDays}.",
        var (min, max) when min > max => "The soonest day cannot be after the latest.",
        _ => null,
    };
}

public record CarrierResponse(string Name, string? TrackingUrlTemplate)
{
    public static CarrierResponse From(Carrier? carrier) => new(carrier?.Name ?? string.Empty, carrier?.TrackingUrlTemplate);
}

public record DeliverySettingsResponse(List<DeliveryOptionResponse> Options, CarrierResponse Carrier);

/// <summary>Staff: every delivery option, offered or not, and the carrier.</summary>
public record GetDeliverySettingsQuery : IRequest<DeliverySettingsResponse>;

/// <summary>Anyone: the carrier's name and tracking template, so a page can link a tracking reference.</summary>
public record GetCarrierQuery : IRequest<CarrierResponse>;

/// <summary>Creates the option under a new code, or changes one; the code itself never changes.</summary>
/// <param name="MinDays">The delivery time in business days, soonest and latest - both or neither (specs/134).</param>
public record SaveDeliveryOptionCommand(
    string Code, string Name, bool IsActive, int SortOrder, Dictionary<string, decimal> Prices, int? MinDays = null, int? MaxDays = null)
    : IRequest<DeliveryOptionResponse>;

public record SaveCarrierCommand(string Name, string? TrackingUrlTemplate) : IRequest<CarrierResponse>;

public partial class SaveDeliveryOptionCommandValidator : AbstractValidator<SaveDeliveryOptionCommand>
{
    public SaveDeliveryOptionCommandValidator(IOptions<CurrencyOptions> money)
    {
        var currencies = money.Value;
        RuleFor(x => x.Code).Must(c => c is not null && CodeShape().IsMatch(c))
            .WithMessage("A code is 1 to 32 lower-case letters, digits or hyphens.");
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("A name is required.").MaximumLength(100);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000);
        RuleFor(x => x).Custom((command, context) =>
        {
            if (DeliveryEstimate.Problem(command.MinDays, command.MaxDays) is { } problem)
                context.AddFailure(command.MinDays is null ? nameof(command.MinDays) : nameof(command.MaxDays), problem);
        });
        RuleFor(x => x.Prices).NotNull();
        RuleForEach(x => x.Prices).Custom((price, context) =>
        {
            var setting = currencies.Supported.FirstOrDefault(c => string.Equals(c.Code, price.Key, StringComparison.OrdinalIgnoreCase));
            if (setting is null)
                context.AddFailure("Prices", $"'{price.Key}' is not a currency this shop sells in.");
            else if (price.Value < 0)
                context.AddFailure("Prices", $"A price in {setting.Code} cannot be negative.");
            else if (price.Value != Math.Round(price.Value, setting.Decimals))
                context.AddFailure("Prices", $"{price.Value} is not an amount in {setting.Code}, which has {setting.Decimals} decimal places.");
        });
        // An option on offer must be sellable in the shop's own currency - the rule configuration was checked for.
        RuleFor(x => x.Prices)
            .Must(prices => prices.Keys.Any(k => string.Equals(k, currencies.DefaultCurrency, StringComparison.OrdinalIgnoreCase)))
            .When(x => x.IsActive && x.Prices is not null)
            .WithMessage($"An option on offer needs a price in {currencies.DefaultCurrency}, the shop's own currency.");
    }

    [GeneratedRegex("^[a-z0-9-]{1,32}$")]
    private static partial Regex CodeShape();
}

public class SaveCarrierCommandValidator : AbstractValidator<SaveCarrierCommand>
{
    public SaveCarrierCommandValidator()
    {
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The carrier needs a name.").MaximumLength(100);
        RuleFor(x => x.TrackingUrlTemplate)
            .MaximumLength(500)
            .Must(t => string.IsNullOrWhiteSpace(t) || t.Contains("{reference}", StringComparison.Ordinal))
            .WithMessage("The tracking address must say where the reference goes: {reference}.")
            .Must(t => string.IsNullOrWhiteSpace(t) || (Uri.TryCreate(t.Replace("{reference}", "x", StringComparison.Ordinal), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            .WithMessage("The tracking address must be an absolute http or https address.");
    }
}

public class DeliveryHandlers(IDeliveryRepository delivery, IAuditTrail audit) :
    IRequestHandler<GetDeliverySettingsQuery, DeliverySettingsResponse>,
    IRequestHandler<GetCarrierQuery, CarrierResponse>,
    IRequestHandler<SaveDeliveryOptionCommand, DeliveryOptionResponse>,
    IRequestHandler<SaveCarrierCommand, CarrierResponse>
{
    private readonly IDeliveryRepository _delivery = delivery;
    private readonly IAuditTrail _audit = audit;

    public async Task<DeliverySettingsResponse> Handle(GetDeliverySettingsQuery request, CancellationToken cancellationToken) =>
        new((await _delivery.GetOptionsAsync(cancellationToken)).Select(DeliveryOptionResponse.From).ToList(),
            CarrierResponse.From(await _delivery.GetCarrierAsync(cancellationToken)));

    public async Task<CarrierResponse> Handle(GetCarrierQuery request, CancellationToken cancellationToken) =>
        CarrierResponse.From(await _delivery.GetCarrierAsync(cancellationToken));

    public async Task<DeliveryOptionResponse> Handle(SaveDeliveryOptionCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var option = await _delivery.FindOptionAsync(request.Code, cancellationToken);
        var before = option is null ? null : DeliveryOptionResponse.From(option);

        // A shop that offers no delivery cannot sell: the last option on offer stays on.
        if (option is { IsActive: true } && !request.IsActive && await _delivery.CountOfferedAsync(option.Code, cancellationToken) == 0)
            throw new ConflictException("This is the last delivery option on offer; turn another on first.");

        if (option is null)
        {
            option = new DeliveryOption { Code = request.Code, CreatedAt = now };
            _delivery.AddOption(option);
        }

        option.Name = request.Name.Trim();
        option.IsActive = request.IsActive;
        option.SortOrder = request.SortOrder;
        option.MinDays = request.MinDays;
        option.MaxDays = request.MaxDays;
        option.UpdatedAt = now;
        var wanted = request.Prices.ToDictionary(p => p.Key.Trim().ToUpperInvariant(), p => p.Value);
        option.Prices.RemoveAll(p => !wanted.ContainsKey(p.Currency));
        foreach (var (currency, amount) in wanted)
        {
            var price = option.Prices.FirstOrDefault(p => p.Currency == currency);
            if (price is null)
                option.Prices.Add(new DeliveryOptionPrice { OptionCode = option.Code, Currency = currency, Amount = amount });
            else
                price.Amount = amount;
        }

        var after = DeliveryOptionResponse.From(option);
        await _audit.RecordAsync(AuditCategory.Order, "DeliveryOptionSaved", "DeliveryOption", option.Code,
            $"Delivery option \"{option.Name}\" saved", before, after, cancellationToken: cancellationToken);
        await _delivery.SaveChangesAsync(cancellationToken);
        return after;
    }

    public async Task<CarrierResponse> Handle(SaveCarrierCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _delivery.GetCarrierAsync(cancellationToken);
        var before = carrier is null ? null : CarrierResponse.From(carrier);
        if (carrier is null)
        {
            carrier = new Carrier();
            _delivery.AddCarrier(carrier);
        }

        carrier.Name = request.Name.Trim();
        carrier.TrackingUrlTemplate = string.IsNullOrWhiteSpace(request.TrackingUrlTemplate) ? null : request.TrackingUrlTemplate.Trim();
        carrier.UpdatedAt = DateTime.UtcNow;

        var after = CarrierResponse.From(carrier);
        await _audit.RecordAsync(AuditCategory.Order, "CarrierSaved", "Carrier", Carrier.TheCarrier.ToString(),
            $"The carrier is \"{carrier.Name}\"", before, after, cancellationToken: cancellationToken);
        await _delivery.SaveChangesAsync(cancellationToken);
        return after;
    }
}
