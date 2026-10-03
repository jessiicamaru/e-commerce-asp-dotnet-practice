using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Payment.Application.Payments.Checkout;

/// <summary>
/// How an order's payment stands, for its owner (specs/143): waiting at the gateway with a freshly signed link, or
/// decided. The storefront polls it while the order waits.
/// </summary>
/// <param name="IpAddress">The customer's address, as the gateway asks for it (<c>vnp_IpAddr</c>).</param>
/// <param name="Language">The page's language for the gateway: "vn" or "en".</param>
public record GetPaymentCheckoutQuery(Guid OrderId, string IpAddress = "127.0.0.1", string Language = "vn")
    : IRequest<PaymentCheckoutResponse>;

/// <param name="State">Preparing, AwaitingPayment, Expired, Paid or Failed.</param>
public record PaymentCheckoutResponse(string Provider, string State, string? PayUrl, DateTime? ExpiresAt);

public static class PaymentCheckoutStates
{
    public const string Preparing = "Preparing";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Expired = "Expired";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
}

public class GetPaymentCheckoutQueryHandler(
    IPaymentRepository repository,
    IPaymentGateway gateway,
    ICurrentUser currentUser,
    IVnPay vnPay) : IRequestHandler<GetPaymentCheckoutQuery, PaymentCheckoutResponse>
{
    private readonly IPaymentRepository _repository = repository;
    private readonly IPaymentGateway _gateway = gateway;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IVnPay _vnPay = vnPay;

    public async Task<PaymentCheckoutResponse> Handle(GetPaymentCheckoutQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");

        var payment = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        var checkout = payment is null ? await _repository.GetCheckoutByOrderIdAsync(request.OrderId, cancellationToken) : null;

        // Payment learns who owns an order only when the saga asks it to charge. Before that it has nothing to show
        // anybody - and nothing to hide either - so the answer is the same for everyone and carries no data.
        var owner = payment?.UserId ?? checkout?.UserId;
        if (owner is null)
        {
            return new PaymentCheckoutResponse(_gateway.ProviderName, PaymentCheckoutStates.Preparing, null, null);
        }

        // Somebody else's order is not found - the same answer as one that does not exist (#39).
        if (owner != me)
        {
            throw new NotFoundException("Payment not found.");
        }

        if (payment is not null)
        {
            return new PaymentCheckoutResponse(
                payment.Provider,
                payment.Status == PaymentStatus.Approved ? PaymentCheckoutStates.Paid : PaymentCheckoutStates.Failed,
                null,
                null);
        }

        var now = DateTime.UtcNow;
        if (now >= checkout!.ExpiresAt)
        {
            return new PaymentCheckoutResponse(checkout.Provider, PaymentCheckoutStates.Expired, null, checkout.ExpiresAt);
        }

        // Signed now, for this request: the link carries its own creation time and the customer's address.
        var payUrl = _vnPay.BuildPayUrl(checkout, request.IpAddress, request.Language, now);

        return new PaymentCheckoutResponse(checkout.Provider, PaymentCheckoutStates.AwaitingPayment, payUrl, checkout.ExpiresAt);
    }
}
