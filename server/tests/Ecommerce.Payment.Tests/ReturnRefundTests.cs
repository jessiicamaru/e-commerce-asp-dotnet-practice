using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Application.Payments.Queries.GetPaymentByOrderId;
using Ecommerce.Payment.Application.Payments.RefundOrder;
using Ecommerce.Payment.Application.Payments.RefundReturn;
using Ecommerce.Payment.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// A returned parcel's refund (specs/066): the amount Order computed, once per return, never more than was
/// paid - and beside, not instead of, the one refund of a whole order.
/// </summary>
[Collection(nameof(PaymentTestCollection))]
public class ReturnRefundTests(PaymentTestFixture fixture)
{
    private readonly PaymentTestFixture _fixture = fixture;

    [Fact]
    public async Task A_returned_parcel_is_refunded_its_amount_once()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 5_000m, "VND");
        var returnId = Guid.CreateVersion7();

        var recorded = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            SendAsync(new RefundReturnCommand(returnId, orderId, 2_200m, "VND"))));

        Assert.Equal(1, recorded.Count(r => r));
        var refund = Assert.Single(await RefundsAsync(orderId));
        Assert.Equal((returnId, 2_200m, "VND", "Stub"), (refund.ReturnId, refund.Amount, refund.Currency, refund.Provider));
    }

    [Fact]
    public async Task Two_parcels_of_one_order_are_refunded_separately()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 5_000m, "VND");

        Assert.True(await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), orderId, 2_200m, "VND")));
        Assert.True(await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), orderId, 1_100m, "VND")));

        Assert.Equal(3_300m, (await RefundsAsync(orderId)).Sum(r => r.Amount));
    }

    [Fact]
    public async Task Never_more_than_was_paid_nor_in_another_currency()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 3_000m, "VND");

        Assert.True(await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), orderId, 2_000m, "VND")));
        Assert.False(await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), orderId, 1_500m, "VND")));
        Assert.False(await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), orderId, 1m, "USD")));

        Assert.Single(await RefundsAsync(orderId));
    }

    [Fact]
    public async Task Nothing_is_refunded_for_an_order_never_charged()
    {
        Assert.False(await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), 100m, "VND")));
    }

    /// <summary>A parcel refund must not stop a whole-order refund being recognised, nor be taken for one.</summary>
    [Fact]
    public async Task A_parcel_refund_is_not_the_order_s_refund()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 5_000m, "VND");
        await SendAsync(new RefundReturnCommand(Guid.CreateVersion7(), orderId, 2_200m, "VND"));

        var payment = await SendAsync(new GetPaymentByOrderIdQuery(orderId));
        Assert.Null(payment.RefundedAt);
    }

    private Task<ChargeOrderResult> ChargeAsync(Guid orderId, decimal amount, string currency) =>
        SendAsync(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), amount, currency));

    private async Task<List<Domain.Entities.Refund>> RefundsAsync(Guid orderId)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentDbContext>()
            .Refunds.AsNoTracking().Where(r => r.OrderId == orderId).ToListAsync();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
