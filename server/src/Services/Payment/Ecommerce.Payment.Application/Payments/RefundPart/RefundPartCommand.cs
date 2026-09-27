using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Entities;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Shared.Audit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payment.Application.Payments.RefundPart;

/// <summary>
/// Records the refund of one part of an order, cancelled before it shipped while the rest goes on (specs/104):
/// <paramref name="Amount"/>, which Order computed from the prices it froze - the part's goods less their discounts,
/// plus their tax. Once per part.
/// </summary>
public record RefundPartCommand(Guid PartId, Guid OrderId, decimal Amount, string Currency) : IRequest<bool>;

/// <remarks>
/// The same shape as a returned parcel's refund (specs/066): the amount comes from the event, and this service checks
/// what it owns - that it took the money, in that currency, and that the refunds would not exceed it. Once, by the
/// database: <c>refunds.PartId</c> is unique.
/// </remarks>
public class RefundPartCommandHandler(
    IPaymentRepository payments,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ILogger<RefundPartCommandHandler> logger) : IRequestHandler<RefundPartCommand, bool>
{
    private readonly IPaymentRepository _payments = payments;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAuditTrail _audit = audit;
    private readonly ILogger<RefundPartCommandHandler> _logger = logger;

    public async Task<bool> Handle(RefundPartCommand request, CancellationToken cancellationToken)
    {
        if (await _payments.GetPartRefundAsync(request.PartId, cancellationToken) is not null)
        {
            return false;
        }

        // A part the buyer paid nothing for (a voucher took all of it) has nothing to give back - and a zero-amount row
        // would break the refunds CHECK, sending the message to the error queue for ever.
        if (request.Amount <= 0)
        {
            _logger.LogInformation("Part {PartId} of order {OrderId} cost the buyer nothing; nothing to refund.", request.PartId, request.OrderId);
            return false;
        }

        var payment = await _payments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (payment is null || payment.Status != PaymentStatus.Approved)
        {
            _logger.LogError("Part {PartId} of order {OrderId} was cancelled, but no approved payment exists to refund.",
                request.PartId, request.OrderId);
            return false;
        }

        // Never more than was taken, in the currency it was taken in: a mistake here would pay money out.
        var refunded = await _payments.GetRefundedTotalAsync(request.OrderId, cancellationToken);
        if (!string.Equals(payment.Currency, request.Currency, StringComparison.OrdinalIgnoreCase)
            || refunded + request.Amount > payment.Amount)
        {
            _logger.LogError(
                "Refused to refund {Amount} {Currency} for part {PartId}: the payment was {Paid} {PaidCurrency}, {Refunded} already refunded.",
                request.Amount, request.Currency, request.PartId, payment.Amount, payment.Currency, refunded);
            return false;
        }

        try
        {
            await _payments.AddRefundAsync(new Refund
            {
                Id = Guid.CreateVersion7(),
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                PartId = request.PartId,
                Amount = request.Amount,
                Currency = payment.Currency,
                // The same provider that took it - "Stub" today, so this moved no money either.
                Provider = payment.Provider,
                RefundedAt = DateTime.UtcNow,
            }, cancellationToken);

            await _audit.RecordAsync(
                AuditCategory.Payment, "RefundRecorded", "Order", payment.OrderId.ToString(),
                $"Refunded {request.Amount} {payment.Currency} through {payment.Provider}: part of the order was cancelled",
                after: new { PaymentId = payment.Id, request.PartId, request.Amount, payment.Currency, payment.Provider },
                cancellationToken: cancellationToken);
            await _payments.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            _unitOfWork.DiscardPendingChanges();
            _logger.LogInformation("Part {PartId} was already refunded by a concurrent delivery.", request.PartId);
            return false;
        }

        _logger.LogWarning("Refund of {Amount} {Currency} recorded for part {PartId} through {Provider} - no money moved if Stub",
            request.Amount, payment.Currency, request.PartId, payment.Provider);
        return true;
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().GetProperty("SqlState")?.GetValue(current) as string == "23505")
            {
                return true;
            }
        }

        return false;
    }
}
