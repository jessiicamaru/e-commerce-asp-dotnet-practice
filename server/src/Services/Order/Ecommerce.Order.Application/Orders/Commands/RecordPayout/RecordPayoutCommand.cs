using Ecommerce.Order.Application.Common.Interfaces;
using Ecommerce.Order.Application.Orders.Common;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Order.Application.Orders.Commands.RecordPayout;

/// <summary>
/// Staff: record that the shop settled everything due to one seller in one currency (specs/037). Moves
/// no money - Payment is a stub - it is the ledger entry.
/// </summary>
/// <remarks>
/// There is no amount here, and there must never be one: the amount is whatever the parts claimed by
/// this payout add up to. An amount in the body would let the ledger say something the parts do not.
/// </remarks>
public record RecordPayoutCommand(Guid SellerId, string Currency) : IRequest<PayoutResponse>;

public class RecordPayoutCommandValidator : AbstractValidator<RecordPayoutCommand>
{
    public RecordPayoutCommandValidator()
    {
        RuleFor(x => x.SellerId).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public class RecordPayoutCommandHandler(
    IPayoutRepository payouts,
    ICurrentUser currentUser,
    ILogger<RecordPayoutCommandHandler> logger)
    : IRequestHandler<RecordPayoutCommand, PayoutResponse>
{
    private readonly IPayoutRepository _payouts = payouts;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RecordPayoutCommandHandler> _logger = logger;

    public async Task<PayoutResponse> Handle(RecordPayoutCommand request, CancellationToken cancellationToken)
    {
        // Who recorded it comes from the token, like every other "who" in this codebase (Constitution IV).
        var admin = _currentUser.Id
            ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var currency = request.Currency.Trim().ToUpperInvariant();

        var payout = await _payouts.TryRecordAsync(
            Guid.CreateVersion7(), request.SellerId, currency, admin, DateTime.UtcNow, cancellationToken)
            ?? throw new ConflictException(Payouts.NothingDue(currency));

        _logger.LogInformation(
            "Payout {PayoutId} recorded by {AdminId}: {Amount} {Currency} to seller {SellerId} over {PartCount} part(s)",
            payout.Id, admin, payout.Amount, payout.Currency, payout.SellerId, payout.PartCount);

        return payout;
    }
}
