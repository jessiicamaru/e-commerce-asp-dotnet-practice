using Ecommerce.Cart.Application.Common;
using Ecommerce.Cart.Application.Common.Interfaces;
using Ecommerce.Cart.Domain.Entities;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Localization;
using Ecommerce.Shared.Money;
using FluentValidation;
using MediatR;

namespace Ecommerce.Cart.Application.Carts;

// A cart before signing in (specs/162, #370). The browser keeps the lines; Cart prices them without storing them, and
// merges them into the account's cart when its shopper signs in.

/// <summary>Prices the browser's lines exactly as a stored cart is priced. Anonymous, and stores nothing.</summary>
public record PriceCartLinesQuery(List<CartLineInput> Lines) : IRequest<CartResponse>;

/// <summary>
/// Merges the browser's lines into the caller's cart: added when absent, raised to the browser's quantity when the cart
/// holds fewer, never lowered - so the same merge twice changes nothing (research D2).
/// </summary>
public record MergeCartCommand(List<CartLineInput> Lines) : IRequest;

public static class GuestCartLimits
{
    /// <summary>One request's worth of lines: a bound on the Catalog call behind an anonymous request (research D4).</summary>
    public const int MaxLines = 50;

    public const int MaxQuantity = 999;

    public static void Lines<T>(AbstractValidator<T> validator, Func<T, List<CartLineInput>> lines)
    {
        validator.RuleFor(x => lines(x)).NotNull()
            .Must(l => l is null || l.Count <= MaxLines).WithMessage($"A cart holds at most {MaxLines} lines.")
            .OverridePropertyName("Lines");
        validator.RuleForEach(x => lines(x) ?? new List<CartLineInput>()).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, MaxQuantity);
        }).OverridePropertyName("Lines");
    }
}

public class PriceCartLinesQueryValidator : AbstractValidator<PriceCartLinesQuery>
{
    public PriceCartLinesQueryValidator() => GuestCartLimits.Lines(this, x => x.Lines);
}

public class MergeCartCommandValidator : AbstractValidator<MergeCartCommand>
{
    public MergeCartCommandValidator() => GuestCartLimits.Lines(this, x => x.Lines);
}

public class GuestCartHandlers(
    ICartRepository carts,
    IUnitOfWork unitOfWork,
    ICatalogProducts catalog,
    ICurrentUser currentUser,
    IRequestLanguage language,
    IRequestCurrency currency) :
    IRequestHandler<PriceCartLinesQuery, CartResponse>,
    IRequestHandler<MergeCartCommand>
{
    private readonly ICartRepository _carts = carts;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICatalogProducts _catalog = catalog;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IRequestLanguage _language = language;
    private readonly IRequestCurrency _currency = currency;

    public Task<CartResponse> Handle(PriceCartLinesQuery request, CancellationToken cancellationToken) =>
        // One line per shape, as a stored cart keeps them: a browser that sent the same shape twice reads as one line.
        CartPricing.PriceAsync(OnePerShape(request.Lines), _catalog, _language.Current, _currency.Current.Code, cancellationToken);

    public async Task Handle(MergeCartCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
        var lines = OnePerShape(request.Lines);
        if (lines.Count == 0)
        {
            return;
        }

        // Under the cart's own lock, like every change to it: a merge and an add at once serialise.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var cart = await _carts.GetOrCreateForUpdateAsync(userId, ct);
            var now = DateTime.UtcNow;

            foreach (var line in lines)
            {
                var held = cart.Lines.FirstOrDefault(l => l.SellableId == line.SellableId);
                if (held is null)
                {
                    cart.Lines.Add(new CartLine
                    {
                        Id = Guid.CreateVersion7(),
                        CartId = cart.Id,
                        ProductId = line.ProductId,
                        VariantId = line.VariantId,
                        Quantity = line.Quantity,
                        AddedAt = now,
                    });
                }
                else if (held.Quantity < line.Quantity)
                {
                    // The larger of the two, never the sum: a repeated merge must change nothing (research D2).
                    held.Quantity = line.Quantity;
                }
            }

            cart.UpdatedAt = now;
            await _carts.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <summary>The same shape twice is one line with the larger quantity, in the order first seen.</summary>
    private static List<CartLineInput> OnePerShape(List<CartLineInput> lines) =>
        lines.GroupBy(l => l.SellableId)
            .Select(g => g.First() with { Quantity = g.Max(l => l.Quantity) })
            .ToList();
}
