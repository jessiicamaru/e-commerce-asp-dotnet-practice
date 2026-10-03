using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payment.Infrastructure.Gateway;

/// <summary>
/// VNPay as the provider (specs/143): it decides nothing when the saga asks - the customer pays on VNPay's page, and
/// VNPay's signed notification decides. Sandbox unless <c>VNPAY_LIVE</c> says otherwise, and the rows and health say which.
/// </summary>
public class VnPayGateway(IOptions<VnPayOptions> options) : IPaymentGateway
{
    public const string Currency = "VND";

    private readonly VnPayOptions _options = options.Value;

    public string ProviderName => _options.Live ? Domain.Entities.Payment.VnPayProvider : Domain.Entities.Payment.VnPaySandboxProvider;

    public string Description => _options.Live ? "VnPay" : "VnPay sandbox";

    public bool MovesMoney => _options.Live;

    public string HealthOutcome => "Customer";

    public GatewayDecision Begin(Guid orderId, Guid userId, decimal amount, string currency)
    {
        // VNPay settles in dong only, and this shop converts nothing (specs/022): an order in another currency cannot be
        // paid here, and is refused like a declined card rather than charged by something else.
        if (!string.Equals(currency, Currency, StringComparison.OrdinalIgnoreCase))
        {
            return GatewayDecision.Decided(PaymentStatus.Rejected, $"VNPay settles in VND only; this order is in {currency}.");
        }

        return GatewayDecision.AwaitCustomer(TimeSpan.FromMinutes(_options.PaymentWindowMinutes));
    }
}
