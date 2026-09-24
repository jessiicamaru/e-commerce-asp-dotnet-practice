using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Entities;
using Ecommerce.Payment.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Ecommerce.Shared.Audit;

namespace Ecommerce.Payment.Application.Payments.RefundOrder;

/// <summary>
/// Records the refund of a cancelled order (specs/039): the whole of what was charged, in the currency it
/// was charged in, once.
/// </summary>
/// <remarks>
/// <para>
/// The amount comes from the PAYMENT row, not from the event - the event carries none on purpose
/// (research D1). What was taken is what this service recorded taking.
/// </para>
/// <para>
/// ⚠️ Once, by the database: <c>refunds.OrderId</c> is unique. Two deliveries at once both read "no refund
/// yet"; one insert wins and the other gets 23505, which is answered as "already refunded" - after
/// discarding the failed row, or the next save on this context would try it again (CLAUDE.md gotcha).
/// </para>
/// </remarks>
public class RefundOrderCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    ILogger<RefundOrderCommandHandler> logger
,
    IAuditTrail audit) : IRequestHandler<RefundOrderCommand, bool>
{
    private readonly IAuditTrail _audit = audit;

    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<RefundOrderCommandHandler> _logger = logger;

    public async Task<bool> Handle(RefundOrderCommand request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);

        // Nothing taken, nothing to give back: a rejected payment, or an order this service never charged.
        if (payment is null || payment.Status != PaymentStatus.Approved)
        {
            _logger.LogInformation(
                "No approved payment for order {OrderId}; nothing to refund ({Reason}).", request.OrderId, request.Reason);
            return false;
        }

        if (await _paymentRepository.GetRefundAsync(request.OrderId, cancellationToken) is not null)
        {
            return false;
        }

        try
        {
            await _paymentRepository.AddRefundAsync(new Refund
            {
                Id = Guid.CreateVersion7(),
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                // The same provider that took it - "Stub" today, so this moved no money either.
                Provider = payment.Provider,
                RefundedAt = DateTime.UtcNow
            }, cancellationToken);

            await _audit.RecordAsync(
                AuditCategory.Payment, "RefundRecorded", "Order", payment.OrderId.ToString(),
                $"Refunded {payment.Amount} {payment.Currency} through {payment.Provider}: {request.Reason}",
                after: new { PaymentId = payment.Id, payment.Amount, payment.Currency, payment.Provider, request.Reason },
                cancellationToken: cancellationToken);
            await _paymentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            _unitOfWork.DiscardPendingChanges();
            _logger.LogInformation("Order {OrderId} was already refunded by a concurrent delivery.", request.OrderId);
            return false;
        }

        _logger.LogWarning(
            "Refund of {Amount} {Currency} recorded for order {OrderId} through {Provider} ({Reason}) - no money moved if Stub",
            payment.Amount, payment.Currency, payment.OrderId, payment.Provider, request.Reason);
        return true;
    }

    /// <summary>PostgreSQL SQLSTATE 23505 - unique_violation, surfaced through Npgsql.</summary>
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
