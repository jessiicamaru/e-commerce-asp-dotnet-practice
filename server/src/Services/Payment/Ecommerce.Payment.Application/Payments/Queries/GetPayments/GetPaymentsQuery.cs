using Ecommerce.Payment.Application.Common.Models;
using Ecommerce.Payment.Application.Payments.Common;
using MediatR;

namespace Ecommerce.Payment.Application.Payments.Queries.GetPayments;

public record GetPaymentsQuery(
    int PageNumber = 1,
    int PageSize = 12,
    Guid? OrderId = null,
    string? Status = null
) : IRequest<PaginatedList<PaymentResponse>>;
