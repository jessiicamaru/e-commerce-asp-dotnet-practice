using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Application.Payments.Common;
using Ecommerce.Shared.Exceptions;
using MediatR;

namespace Ecommerce.Payment.Application.Payments.Queries.GetPaymentByOrderId;

public class GetPaymentByOrderIdQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentByOrderIdQuery, PaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository = paymentRepository;

    public async Task<PaymentResponse> Handle(GetPaymentByOrderIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"No payment has been recorded for order '{request.OrderId}'.");

        return new PaymentResponse(
            payment.Id, payment.OrderId, payment.UserId, payment.Amount,
            payment.Status.ToString(), payment.FailureReason, payment.Provider, payment.ProcessedAt);
    }
}
