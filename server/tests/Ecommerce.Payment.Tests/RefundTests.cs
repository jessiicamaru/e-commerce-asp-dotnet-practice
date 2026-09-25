using Ecommerce.Shared.Audit;
using Ecommerce.Payment.Application;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Application.Payments.Queries.GetPaymentByOrderId;
using Ecommerce.Payment.Application.Payments.RefundOrder;
using Ecommerce.Payment.Infrastructure.Gateway;
using Ecommerce.Payment.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// A cancelled order's refund (specs/039): once, for what was charged, and only for what was charged.
/// The provider is a stub, so this is a ledger entry - which is exactly why it must be right.
/// </summary>
[Collection(nameof(PaymentTestCollection))]
public class RefundTests(PaymentTestFixture fixture)
{
    private readonly PaymentTestFixture _fixture = fixture;

    [Fact]
    public async Task A_cancelled_order_is_refunded_what_it_was_charged()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 3_333_000m, "VND");

        Assert.True(await SendAsync(new RefundOrderCommand(orderId)));

        var refund = Assert.Single(await RefundsAsync(orderId));
        Assert.Equal(3_333_000m, refund.Amount);
        Assert.Equal("VND", refund.Currency);
        Assert.Equal("Stub", refund.Provider);

        var payment = await SendAsync(new GetPaymentByOrderIdQuery(orderId));
        Assert.Equal(3_333_000m, payment.RefundedAmount);
        Assert.NotNull(payment.RefundedAt);
    }

    /// <summary>Principle III: redelivered, or delivered twice at once - one refund, never two.</summary>
    [Fact]
    public async Task A_cancellation_delivered_many_times_refunds_once()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 100m, "USD");

        var recorded = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendAsync(new RefundOrderCommand(orderId))));

        Assert.Equal(1, recorded.Count(r => r));
        Assert.Single(await RefundsAsync(orderId));
    }

    /// <summary>
    /// ⚠️ The losing side of a race, made to happen every time: this repository never sees an existing
    /// refund, exactly as a delivery does when it reads a moment before another commits. The unique index
    /// is then the only thing between a cancellation and a second refund, and the handler must answer the
    /// violation as "already refunded" rather than fail - and leave nothing half-written behind.
    /// </summary>
    [Fact]
    public async Task A_delivery_that_loses_the_race_is_told_it_was_already_refunded()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 100m, "USD");
        await using var blind = BuildNeverSeesRefundsProvider();

        async Task<bool> RefundAsync()
        {
            await using var scope = blind.CreateAsyncScope();
            var refunded = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RefundOrderCommand(orderId));

            // What the consumer's EF outbox does after every consume: save the same context again. A failed
            // insert still tracked would be re-attempted here and fail the message all over again.
            await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().SaveChangesAsync();
            return refunded;
        }

        Assert.True(await RefundAsync());
        Assert.False(await RefundAsync());
        Assert.Single(await RefundsAsync(orderId));
    }

    /// <summary>Nothing was taken, so nothing is given back - not a refund of zero, no row at all.</summary>
    [Fact]
    public async Task A_payment_that_was_rejected_is_not_refunded()
    {
        var orderId = Guid.CreateVersion7();
        await using (var rejecting = _fixture.BuildRejectingProvider())
        await using (var scope = rejecting.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), 50m, "USD"));
        }

        Assert.False(await SendAsync(new RefundOrderCommand(orderId)));
        Assert.Empty(await RefundsAsync(orderId));
    }

    /// <summary>
    /// specs/053: the saga gave up on the payment and failed the order, then Payment approved it. The same
    /// once-only refund, and the audit log says why.
    /// </summary>
    [Fact]
    public async Task A_payment_approved_after_the_order_failed_is_refunded_once_and_says_why()
    {
        var orderId = Guid.CreateVersion7();
        await ChargeAsync(orderId, 2_450_000m, "VND");
        const string why = "The payment was approved after the order had failed waiting for it.";

        Assert.True(await SendAsync(new RefundOrderCommand(orderId, why)));
        Assert.False(await SendAsync(new RefundOrderCommand(orderId, why)));

        var refund = Assert.Single(await RefundsAsync(orderId));
        Assert.Equal((2_450_000m, "VND"), (refund.Amount, refund.Currency));
        var entry = Assert.Single(_fixture.Harness.Published.Select<Ecommerce.Contracts.Activity.AuditEntryRecorded>()
            .Select(x => x.Context.Message).Where(e => e.SubjectId == orderId.ToString() && e.Action == "RefundRecorded"));
        Assert.Contains(why, entry.Summary);
    }

    [Fact]
    public async Task An_order_this_service_never_charged_is_a_no_op()
    {
        Assert.False(await SendAsync(new RefundOrderCommand(Guid.CreateVersion7())));
    }

    // ------------------------------------------------------------------ helpers

    private ServiceProvider BuildNeverSeesRefundsProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                ["Payment:Outcome"] = PaymentOutcomeOptions.ApproveValue
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<PaymentDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.Configure<PaymentOutcomeOptions>(configuration.GetSection(PaymentOutcomeOptions.SectionName));
        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPaymentRepository>(sp =>
            new NeverSeesRefunds(new PaymentRepository(sp.GetRequiredService<PaymentDbContext>())));
        services.AddMassTransitTestHarness();
        services.AddAuditTrail("payment");
        return services.BuildServiceProvider(true);
    }

    private sealed class NeverSeesRefunds(IPaymentRepository inner) : IPaymentRepository
    {
        public Task<Domain.Entities.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
            => inner.GetByOrderIdAsync(orderId, ct);

        public Task<(List<Domain.Entities.Payment> Items, int TotalCount)> GetPaginatedAsync(
            int pageNumber, int pageSize, Guid? orderId, string? status, CancellationToken ct = default)
            => inner.GetPaginatedAsync(pageNumber, pageSize, orderId, status, ct);

        public Task AddAsync(Domain.Entities.Payment payment, CancellationToken ct = default) => inner.AddAsync(payment, ct);

        public Task<Domain.Entities.Refund?> GetRefundAsync(Guid orderId, CancellationToken ct = default)
            => Task.FromResult<Domain.Entities.Refund?>(null);

        public Task<Dictionary<Guid, Domain.Entities.Refund>> GetRefundsAsync(
            IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
            => inner.GetRefundsAsync(orderIds, ct);

        public Task AddRefundAsync(Domain.Entities.Refund refund, CancellationToken ct = default) => inner.AddRefundAsync(refund, ct);

        public Task<Domain.Entities.Refund?> GetReturnRefundAsync(Guid returnId, CancellationToken ct = default)
            => Task.FromResult<Domain.Entities.Refund?>(null);

        public Task<decimal> GetRefundedTotalAsync(Guid orderId, CancellationToken ct = default)
            => inner.GetRefundedTotalAsync(orderId, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
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
