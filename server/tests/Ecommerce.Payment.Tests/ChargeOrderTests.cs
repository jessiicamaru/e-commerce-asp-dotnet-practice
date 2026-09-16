using Ecommerce.Contracts.Payment;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Domain.Enums;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Payment.Tests;

[Collection(nameof(PaymentTestCollection))]
public class ChargeOrderTests(PaymentTestFixture fixture)
{
    private readonly PaymentTestFixture _fixture = fixture;

    private async Task<ChargeOrderResult> ChargeAsync(Guid orderId, decimal amount, ServiceProvider? provider = null)
    {
        await using var scope = (provider ?? _fixture.Services).CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        return await mediator.Send(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), amount));
    }

    private async Task<Domain.Entities.Payment?> ReadAsync(Guid orderId)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider
            .GetRequiredService<IPaymentRepository>()
            .GetByOrderIdAsync(orderId);
    }

    // ---------- User Story 1 ----------

    [Fact]
    public async Task A_valid_request_is_approved_and_recorded()
    {
        var orderId = Guid.CreateVersion7();

        var result = await ChargeAsync(orderId, 150_000m);

        Assert.True(result.Approved);

        var payment = await ReadAsync(orderId);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(150_000m, payment.Amount);
    }

    [Fact]
    public async Task Every_payment_records_which_provider_produced_it()
    {
        var orderId = Guid.CreateVersion7();

        await ChargeAsync(orderId, 1_000m);

        // A row that does not say where its outcome came from is one somebody will later assume
        // came from a bank. This is one of the three signals from research D3.
        var payment = await ReadAsync(orderId);
        Assert.Equal("Stub", payment!.Provider);
    }

    [Fact]
    public async Task An_approval_publishes_one_reply_and_never_both()
    {
        var orderId = Guid.CreateVersion7();

        await ChargeAsync(orderId, 2_000m);

        Assert.True(await _fixture.Harness.Published.Any<PaymentProcessedEvent>(
            x => x.Context.Message.OrderId == orderId));

        Assert.False(await _fixture.Harness.Published.Any<PaymentFailedEvent>(
            x => x.Context.Message.OrderId == orderId));
    }

    [Fact]
    public async Task The_reported_payment_id_matches_the_recorded_row()
    {
        var orderId = Guid.CreateVersion7();

        var result = await ChargeAsync(orderId, 500m);

        // The saga is handed this id; it has to lead somewhere real.
        var payment = await ReadAsync(orderId);
        Assert.Equal(payment!.Id, result.PaymentId);
    }

    // ---------- User Story 3: replay and concurrency ----------

    [Fact]
    public async Task Replaying_a_request_records_one_payment()
    {
        var orderId = Guid.CreateVersion7();

        var first = await ChargeAsync(orderId, 900m);
        await ChargeAsync(orderId, 900m);
        await ChargeAsync(orderId, 900m);

        var payment = await ReadAsync(orderId);
        Assert.Equal(first.PaymentId, payment!.Id);

        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var (items, total) = await repository.GetPaginatedAsync(1, 50, orderId, null);

        Assert.Equal(1, total);
        Assert.Single(items);
    }

    [Fact]
    public async Task A_replay_still_replies_because_the_saga_may_have_missed_the_first()
    {
        var orderId = Guid.CreateVersion7();

        await ChargeAsync(orderId, 700m);
        await ChargeAsync(orderId, 700m);

        var replies = _fixture.Harness.Published
            .Select<PaymentProcessedEvent>()
            .Count(x => x.Context.Message.OrderId == orderId);

        Assert.Equal(2, replies);
    }

    [Fact]
    public async Task Fifty_simultaneous_requests_record_exactly_one_payment()
    {
        var orderId = Guid.CreateVersion7();

        var attempts = Enumerable.Range(0, 50)
            .Select(_ => ChargeAsync(orderId, 1_234m))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        // Every request got an answer — none dropped, which would leave the saga waiting forever.
        Assert.Equal(50, results.Length);

        // And every answer agrees, because the losers report the row that won.
        Assert.Single(results.Select(r => r.PaymentId).Distinct());

        await using var scope = _fixture.NewScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var (_, total) = await repository.GetPaginatedAsync(1, 100, orderId, null);

        Assert.Equal(1, total);
    }

    // ---------- User Story 4 and FR-004: rejection ----------

    [Fact]
    public async Task A_non_positive_amount_is_refused_even_while_approving()
    {
        var orderId = Guid.CreateVersion7();

        var result = await ChargeAsync(orderId, 0m);

        Assert.False(result.Approved);
        Assert.Contains("Invalid amount", result.FailureReason);

        // The refusal is recorded, with the offending amount visible. "Always succeeds" does not
        // mean "approves nonsense", and a rejection nobody can see is not accountable.
        var payment = await ReadAsync(orderId);
        Assert.Equal(PaymentStatus.Rejected, payment!.Status);
        Assert.Equal(0m, payment.Amount);
    }

    [Fact]
    public async Task A_negative_amount_is_refused()
    {
        var result = await ChargeAsync(Guid.CreateVersion7(), -10m);

        Assert.False(result.Approved);
        Assert.Contains("Invalid amount", result.FailureReason);
    }

    [Fact]
    public async Task Configured_to_reject_it_refuses_and_says_why()
    {
        await using var rejecting = _fixture.BuildRejectingProvider();
        var orderId = Guid.CreateVersion7();

        var result = await ChargeAsync(orderId, 5_000m, rejecting);

        Assert.False(result.Approved);

        // The reason names the setting on purpose: whoever reads it in a log should learn this is
        // a configured stand-in refusing, not a provider declining a card.
        Assert.Contains("PAYMENT_OUTCOME=Reject", result.FailureReason);

        var payment = await ReadAsync(orderId);
        Assert.Equal(PaymentStatus.Rejected, payment!.Status);
    }

    [Fact]
    public void An_unrecognised_outcome_fails_at_startup_rather_than_guessing()
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new Ecommerce.Payment.Infrastructure.Gateway.PaymentOutcomeOptions { Outcome = "Maybe" });

        // A typo silently read as "Approve" would mean a service somebody believed was rejecting
        // quietly approving everything instead.
        var exception = Assert.Throws<InvalidOperationException>(
            () => new Ecommerce.Payment.Infrastructure.Gateway.StubPaymentGateway(options));

        Assert.Contains("Maybe", exception.Message);
    }
}
