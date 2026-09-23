using Ecommerce.Contracts.Activity;
using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Application.Payments.RefundOrder;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

/// <summary>What Payment tells the audit log (specs/041): a charge and a refund - once each, however often asked.</summary>
[Collection(nameof(PaymentTestCollection))]
public class AuditTests(PaymentTestFixture fixture)
{
    private readonly PaymentTestFixture _fixture = fixture;

    [Fact]
    public async Task A_charge_and_its_refund_are_each_recorded_once()
    {
        var orderId = Guid.CreateVersion7();

        await SendAsync(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), 1_155_000m, "VND"));
        await SendAsync(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), 1_155_000m, "VND"));   // redelivered
        await SendAsync(new RefundOrderCommand(orderId));
        await SendAsync(new RefundOrderCommand(orderId));                                            // redelivered

        var entries = _fixture.Harness.Published.Select<AuditEntryRecorded>()
            .Select(x => x.Context.Message)
            .Where(e => e.SubjectId == orderId.ToString())
            .ToList();

        Assert.Equal(["PaymentCharged", "RefundRecorded"], entries.OrderBy(e => e.OccurredAt).Select(e => e.Action));
        Assert.All(entries, e => Assert.Equal("Payment", e.Category));
        Assert.Contains("1155000", entries[0].After);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
