using Ecommerce.Contracts.Payment;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payment.Application.Payments.ChargeOrder;

public class ChargeOrderCommandHandler(
    IUnitOfWork unitOfWork,
    IPaymentRepository paymentRepository,
    IPaymentGateway gateway,
    IPublishEndpoint publishEndpoint,
    ILogger<ChargeOrderCommandHandler> logger
) : IRequestHandler<ChargeOrderCommand, ChargeOrderResult>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IPaymentGateway _gateway = gateway;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<ChargeOrderCommandHandler> _logger = logger;

    public async Task<ChargeOrderResult> Handle(ChargeOrderCommand request, CancellationToken cancellationToken)
    {
        ChargeOrderResult? result = null;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(
                ct => ProcessAsync(request, r => result = r, ct), cancellationToken);
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            // Another delivery of this request won the race and inserted first. Dropping the
            // message would leave the saga waiting forever, and deciding independently could
            // contradict what was recorded — so report the outcome that actually won.
            _logger.LogInformation(
                "Concurrent payment for order {OrderId}; reporting the outcome that was recorded.",
                request.OrderId);

            await _unitOfWork.ExecuteInTransactionAsync(
                ct => ReplyWithExistingAsync(request.OrderId, r => result = r, ct), cancellationToken);
        }

        return result!;
    }

    private async Task ProcessAsync(
        ChargeOrderCommand request,
        Action<ChargeOrderResult> capture,
        CancellationToken ct)
    {
        var existing = await _paymentRepository.GetByOrderIdAsync(request.OrderId, ct);

        if (existing is not null)
        {
            // Already decided. Reply again anyway: the saga may never have seen the first reply,
            // and one it has already handled is discarded by correlation.
            await PublishAsync(existing, ct);
            capture(ToResult(existing));
            return;
        }

        var (status, failureReason) = request.Amount <= 0
            ? (PaymentStatus.Rejected, $"Invalid amount {request.Amount} for order {request.OrderId}")
            : _gateway.Charge(request.OrderId, request.UserId, request.Amount);

        var payment = new Domain.Entities.Payment
        {
            Id = Guid.CreateVersion7(),
            OrderId = request.OrderId,
            UserId = request.UserId,
            Amount = request.Amount,
            Status = status,
            FailureReason = failureReason,
            Provider = _gateway.ProviderName,
            ProcessedAt = DateTime.UtcNow
        };

        await _paymentRepository.AddAsync(payment, ct);

        // Publish BEFORE the single SaveChangesAsync, so the payment row and the outbox entry
        // commit together. Publishing afterwards would allow a reply describing a payment the
        // database never accepted, or a payment nobody is told about.
        await PublishAsync(payment, ct);

        await _paymentRepository.SaveChangesAsync(ct);

        capture(ToResult(payment));
    }

    private async Task ReplyWithExistingAsync(
        Guid orderId,
        Action<ChargeOrderResult> capture,
        CancellationToken ct)
    {
        var existing = await _paymentRepository.GetByOrderIdAsync(orderId, ct)
            ?? throw new InvalidOperationException(
                $"A unique violation was raised for order {orderId} but no payment could be read back.");

        await PublishAsync(existing, ct);
        await _paymentRepository.SaveChangesAsync(ct);

        capture(ToResult(existing));
    }

    private async Task PublishAsync(Domain.Entities.Payment payment, CancellationToken ct)
    {
        if (payment.Status == PaymentStatus.Approved)
        {
            await _publishEndpoint.Publish(
                new PaymentProcessedEvent(payment.OrderId, payment.Id, payment.ProcessedAt), ct);
        }
        else
        {
            await _publishEndpoint.Publish(
                new PaymentFailedEvent(payment.OrderId, payment.FailureReason ?? "Payment rejected"), ct);
        }
    }

    private static ChargeOrderResult ToResult(Domain.Entities.Payment payment) =>
        new(payment.Id, payment.Status == PaymentStatus.Approved, payment.FailureReason);

    /// <summary>PostgreSQL SQLSTATE 23505 — unique_violation, surfaced through Npgsql.</summary>
    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;

            if (sqlState == "23505")
            {
                return true;
            }
        }

        return false;
    }
}
