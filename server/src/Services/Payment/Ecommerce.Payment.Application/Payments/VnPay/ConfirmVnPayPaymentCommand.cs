using Ecommerce.Contracts.Payment;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Entities;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Shared.Audit;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Payment.Application.Payments.VnPay;

/// <summary>
/// VNPay's IPN (specs/143): the gateway's signed, server-to-server word on a payment. The address is public, so its
/// authority is the signature alone - and it is answered in VNPay's codes, which the gateway retries on until it reads
/// "00" or "02".
/// </summary>
public record ConfirmVnPayPaymentCommand(IReadOnlyDictionary<string, string> Query) : IRequest<VnPayIpnAnswer>;

public record VnPayIpnAnswer(string RspCode, string Message)
{
    public static readonly VnPayIpnAnswer Confirmed = new("00", "Confirm Success");
    public static readonly VnPayIpnAnswer OrderNotFound = new("01", "Order not found");
    public static readonly VnPayIpnAnswer AlreadyConfirmed = new("02", "Order already confirmed");
    public static readonly VnPayIpnAnswer InvalidAmount = new("04", "Invalid amount");
    public static readonly VnPayIpnAnswer InvalidSignature = new("97", "Invalid signature");
    public static readonly VnPayIpnAnswer Unknown = new("99", "Unknown error");
}

public class ConfirmVnPayPaymentCommandHandler(
    IVnPay vnPay,
    IUnitOfWork unitOfWork,
    IPaymentRepository repository,
    IPublishEndpoint publishEndpoint,
    IAuditTrail audit,
    ILogger<ConfirmVnPayPaymentCommandHandler> logger) : IRequestHandler<ConfirmVnPayPaymentCommand, VnPayIpnAnswer>
{
    private readonly IVnPay _vnPay = vnPay;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IPaymentRepository _repository = repository;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAuditTrail _audit = audit;
    private readonly ILogger<ConfirmVnPayPaymentCommandHandler> _logger = logger;

    public async Task<VnPayIpnAnswer> Handle(ConfirmVnPayPaymentCommand request, CancellationToken cancellationToken)
    {
        // The signature first: nothing about the shop's orders is looked at, or revealed, for a call nobody signed.
        var notification = _vnPay.Read(request.Query);
        if (notification is null)
        {
            _logger.LogWarning("A VNPay notification with a bad signature or merchant code was refused.");
            return VnPayIpnAnswer.InvalidSignature;
        }

        var answer = VnPayIpnAnswer.Unknown;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct => answer = await ConfirmAsync(notification, ct), cancellationToken);
        }
        catch (Exception exception) when (IsUniqueViolation(exception))
        {
            // Two copies of one notification at once: the other inserted the payment first. Nothing more to do.
            _unitOfWork.DiscardPendingChanges();
            return VnPayIpnAnswer.AlreadyConfirmed;
        }

        return answer;
    }

    private async Task<VnPayIpnAnswer> ConfirmAsync(VnPayNotification notification, CancellationToken ct)
    {
        var checkout = await _repository.GetCheckoutByReferenceAsync(notification.Reference, ct);
        if (checkout is null)
        {
            return VnPayIpnAnswer.OrderNotFound;
        }

        // Exactly what the shop asked for: a signed notification for a different amount is not this payment.
        if (notification.Amount != checkout.Amount)
        {
            _logger.LogWarning(
                "VNPay reported {Reported} for order {OrderId}, which asked for {Expected}; refused.",
                notification.Amount, checkout.OrderId, checkout.Amount);
            return VnPayIpnAnswer.InvalidAmount;
        }

        if (checkout.CompletedAt is not null || await _repository.GetByOrderIdAsync(checkout.OrderId, ct) is not null)
        {
            return VnPayIpnAnswer.AlreadyConfirmed;
        }

        var now = DateTime.UtcNow;

        // One guarded statement decides which copy of the notification records the payment.
        if (!await _repository.ClaimCheckoutAsync(checkout.Id, notification.ResponseCode, notification.TransactionNo, now, ct))
        {
            return VnPayIpnAnswer.AlreadyConfirmed;
        }

        var payment = new Domain.Entities.Payment
        {
            Id = Guid.CreateVersion7(),
            OrderId = checkout.OrderId,
            UserId = checkout.UserId,
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            Status = notification.Succeeded ? PaymentStatus.Approved : PaymentStatus.Rejected,
            FailureReason = notification.Succeeded ? null : DescribeFailure(notification.ResponseCode),
            Provider = checkout.Provider,
            ProviderReference = notification.TransactionNo,
            ProcessedAt = now,
        };
        await _repository.AddAsync(payment, ct);

        // The record, the audit and the saga's reply commit together, before the one save (Principle III).
        await _audit.RecordAsync(
            AuditCategory.Payment,
            payment.Status == PaymentStatus.Approved ? "PaymentCharged" : "PaymentRefused",
            "Order", payment.OrderId.ToString(),
            payment.Status == PaymentStatus.Approved
                ? $"Charged {payment.Amount} {payment.Currency} through {payment.Provider} ({payment.ProviderReference})"
                : $"Refused {payment.Amount} {payment.Currency}: {payment.FailureReason}",
            after: new { payment.Id, payment.Amount, payment.Currency, Status = payment.Status.ToString(), payment.Provider, payment.ProviderReference, payment.FailureReason },
            cancellationToken: ct);

        if (payment.Status == PaymentStatus.Approved)
        {
            await _publishEndpoint.Publish(new PaymentProcessedEvent(payment.OrderId, payment.Id, payment.ProcessedAt), ct);
        }
        else
        {
            await _publishEndpoint.Publish(new PaymentFailedEvent(payment.OrderId, payment.FailureReason!), ct);
        }

        await _repository.SaveChangesAsync(ct);
        return VnPayIpnAnswer.Confirmed;
    }

    /// <summary>VNPay's response codes, in words a person reads on the order and in the audit.</summary>
    public static string DescribeFailure(string code) => code switch
    {
        "24" => "The customer cancelled the payment at VNPay (24).",
        "11" => "The payment window at VNPay expired (11).",
        "51" => "VNPay: the account has insufficient funds (51).",
        "65" => "VNPay: the account is over its daily limit (65).",
        "75" => "VNPay: the bank is under maintenance (75).",
        "79" or "13" => $"VNPay: the payment password was wrong too many times ({code}).",
        _ => $"VNPay declined the payment ({code}).",
    };

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
