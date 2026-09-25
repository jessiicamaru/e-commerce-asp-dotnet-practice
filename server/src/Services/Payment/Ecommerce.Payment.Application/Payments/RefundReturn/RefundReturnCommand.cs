using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Entities;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Shared.Audit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payment.Application.Payments.RefundReturn;

/// <summary>
/// Records the refund of a returned parcel (specs/066): <paramref name="Amount"/>, which Order computed from the
/// prices it froze - the parcel's goods and their tax. Once per return.
/// </summary>
public record RefundReturnCommand(Guid ReturnId, Guid OrderId, decimal Amount, string Currency) : IRequest<bool>;

/// <remarks>
/// <para>
/// Unlike a cancellation, the amount comes FROM THE EVENT: a return is part of an order, and only Order knows
/// which lines that part was and what was paid for them. What this service checks is what it owns - that it
/// took money for the order, in that currency, and that the refunds would not exceed it.
/// </para>
/// <para>
/// ⚠️ Once, by the database: <c>refunds.ReturnId</c> is unique. A redelivery finds the refund and stops; two at
/// once both insert and one gets 23505 - answered as "already refunded", after discarding the failed row
/// (CLAUDE.md gotcha).
/// </para>
/// </remarks>
public class RefundReturnCommandHandler(
    IPaymentRepository payments,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ILogger<RefundReturnCommandHandler> logger) : IRequestHandler<RefundReturnCommand, bool>
{
    private readonly IPaymentRepository _payments = payments;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAuditTrail _audit = audit;
    private readonly ILogger<RefundReturnCommandHandler> _logger = logger;

    public async Task<bool> Handle(RefundReturnCommand request, CancellationToken cancellationToken)
    {
        if (await _payments.GetReturnRefundAsync(request.ReturnId, cancellationToken) is not null)
        {
            return false;
        }

        var payment = await _payments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (payment is null || payment.Status != PaymentStatus.Approved)
        {
            _logger.LogError("Return {ReturnId} of order {OrderId} was received, but no approved payment exists to refund.",
                request.ReturnId, request.OrderId);
            return false;
        }

        // Never more than was taken, in the currency it was taken in: a mistake here would pay money out.
        var refunded = await _payments.GetRefundedTotalAsync(request.OrderId, cancellationToken);
        if (!string.Equals(payment.Currency, request.Currency, StringComparison.OrdinalIgnoreCase)
            || request.Amount <= 0 || refunded + request.Amount > payment.Amount)
        {
            _logger.LogError(
                "Refused to refund {Amount} {Currency} for return {ReturnId}: the payment was {Paid} {PaidCurrency}, {Refunded} already refunded.",
                request.Amount, request.Currency, request.ReturnId, payment.Amount, payment.Currency, refunded);
            return false;
        }

        try
        {
            await _payments.AddRefundAsync(new Refund
            {
                Id = Guid.CreateVersion7(),
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                ReturnId = request.ReturnId,
                Amount = request.Amount,
                Currency = payment.Currency,
                // The same provider that took it - "Stub" today, so this moved no money either.
                Provider = payment.Provider,
                RefundedAt = DateTime.UtcNow,
            }, cancellationToken);

            await _audit.RecordAsync(
                AuditCategory.Payment, "RefundRecorded", "Order", payment.OrderId.ToString(),
                $"Refunded {request.Amount} {payment.Currency} through {payment.Provider}: a parcel was returned",
                after: new { PaymentId = payment.Id, request.ReturnId, request.Amount, payment.Currency, payment.Provider },
                cancellationToken: cancellationToken);
            await _payments.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            _unitOfWork.DiscardPendingChanges();
            _logger.LogInformation("Return {ReturnId} was already refunded by a concurrent delivery.", request.ReturnId);
            return false;
        }

        _logger.LogWarning("Refund of {Amount} {Currency} recorded for return {ReturnId} through {Provider} - no money moved if Stub",
            request.Amount, payment.Currency, request.ReturnId, payment.Provider);
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
