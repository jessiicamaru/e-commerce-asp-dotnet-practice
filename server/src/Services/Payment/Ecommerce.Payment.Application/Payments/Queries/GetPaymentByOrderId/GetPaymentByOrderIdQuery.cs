using Ecommerce.Payment.Application.Payments.Common;
using MediatR;

namespace Ecommerce.Payment.Application.Payments.Queries.GetPaymentByOrderId;

public record GetPaymentByOrderIdQuery(Guid OrderId) : IRequest<PaymentResponse>;
