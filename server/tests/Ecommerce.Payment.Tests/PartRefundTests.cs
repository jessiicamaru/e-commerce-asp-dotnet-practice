using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Application.Payments.Queries.GetPayments;
using Ecommerce.Payment.Application.Payments.RefundOrder;
using Ecommerce.Payment.Application.Payments.RefundPart;
using Ecommerce.Payment.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// A part cancelled on its own is refunded once (specs/104), and a later refund of the whole order is what is left -
/// never the cancelled part a second time.
/// </summary>
[Collection(nameof(PaymentTestCollection))]
public class PartRefundTests(PaymentTestFixture fixture)
{
    private readonly PaymentTestFixture _fixture = fixture;

    [Fact]
    public async Task A_part_is_refunded_once_and_the_whole_order_then_refunds_only_what_is_left()
    {
        var order = Guid.CreateVersion7();
        var part = Guid.CreateVersion7();
        await ChargeAsync(order, 1_000_000m);

        Assert.True(await SendAsync(new RefundPartCommand(part, order, 250_000m, "VND")));
        Assert.False(await SendAsync(new RefundPartCommand(part, order, 250_000m, "VND")));   // redelivered

        Assert.True(await SendAsync(new RefundOrderCommand(order)));   // the rest cancelled
        Assert.False(await SendAsync(new RefundOrderCommand(order)));

        var refunds = await RefundsAsync(order);
        Assert.Equal([250_000m, 750_000m], refunds.OrderBy(r => r.RefundedAt).Select(r => r.Amount));
        Assert.Equal(1_000_000m, refunds.Sum(r => r.Amount));   // exactly what was charged
        Assert.Equal(part, refunds.Single(r => r.Amount == 250_000m).PartId);

        // A page of payments reads each order's whole refund - a part refund beside it is not a second key.
        var page = await SendAsync(new GetPaymentsQuery(OrderId: order));
        Assert.Equal(750_000m, Assert.Single(page.Items).RefundedAmount);
    }

    [Fact]
    public async Task Never_more_than_was_taken_nor_in_another_currency_nor_nothing()
    {
        var order = Guid.CreateVersion7();
        await ChargeAsync(order, 100_000m);

        Assert.False(await SendAsync(new RefundPartCommand(Guid.CreateVersion7(), order, 150_000m, "VND")));
        Assert.False(await SendAsync(new RefundPartCommand(Guid.CreateVersion7(), order, 50_000m, "USD")));
        Assert.False(await SendAsync(new RefundPartCommand(Guid.CreateVersion7(), order, 0m, "VND")));   // a voucher paid for all of it
        Assert.Empty(await RefundsAsync(order));
    }

    // ------------------------------------------------------------------ helpers

    private Task<ChargeOrderResult> ChargeAsync(Guid orderId, decimal amount) =>
        SendAsync(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), amount, "VND"));

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
