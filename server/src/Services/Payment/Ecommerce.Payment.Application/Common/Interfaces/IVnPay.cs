using Ecommerce.Payment.Domain.Entities;

namespace Ecommerce.Payment.Application.Common.Interfaces;

/// <summary>
/// VNPay's protocol (2.1.0), kept out of the handlers (specs/143): the signed link that sends a customer to the gateway,
/// and the reading of the gateway's signed notification.
/// </summary>
public interface IVnPay
{
    /// <summary>A freshly signed link to pay for this checkout.</summary>
    string BuildPayUrl(PaymentCheckout checkout, string ipAddress, string language, DateTime now);

    /// <summary>
    /// The notification, when its signature and merchant code are right; null otherwise - nothing about the shop's data
    /// is looked at for a notification nobody signed.
    /// </summary>
    VnPayNotification? Read(IReadOnlyDictionary<string, string> query);
}

/// <param name="Reference">vnp_TxnRef: the order id in N format.</param>
/// <param name="Amount">vnp_Amount / 100, in dong.</param>
/// <param name="Succeeded">vnp_ResponseCode and vnp_TransactionStatus both "00".</param>
public sealed record VnPayNotification(
    string Reference,
    decimal Amount,
    string ResponseCode,
    string TransactionStatus,
    string? TransactionNo,
    bool Succeeded);
