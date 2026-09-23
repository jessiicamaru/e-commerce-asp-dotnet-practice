using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Application.Common.Models;
using Ecommerce.Payment.Application.Payments.Common;
using MediatR;

namespace Ecommerce.Payment.Application.Payments.Queries.GetPayments;

public class GetPaymentsQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentsQuery, PaginatedList<PaymentResponse>>
{
    private readonly IPaymentRepository _paymentRepository = paymentRepository;

    public async Task<PaginatedList<PaymentResponse>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await _paymentRepository.GetPaginatedAsync(
            pageNumber, pageSize, request.OrderId, request.Status, cancellationToken);

        // The page's refunds (specs/039) in one query, not one per row.
        var refunds = await _paymentRepository.GetRefundsAsync(items.Select(x => x.OrderId).ToList(), cancellationToken);

        var responses = items
            .Select(x => new PaymentResponse(
                x.Id, x.OrderId, x.UserId, x.Amount,
                x.Status.ToString(), x.FailureReason, x.Provider, x.ProcessedAt,
                refunds.GetValueOrDefault(x.OrderId)?.Amount,
                refunds.GetValueOrDefault(x.OrderId)?.RefundedAt))
            .ToList();

        return new PaginatedList<PaymentResponse>(responses, totalCount, pageNumber, pageSize);
    }
}
