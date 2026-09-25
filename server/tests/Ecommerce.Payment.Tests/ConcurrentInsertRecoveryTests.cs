using Ecommerce.Shared.Audit;
using Ecommerce.Payment.Application;
using Ecommerce.Payment.Application.Common.Interfaces;
using Ecommerce.Payment.Application.Payments.ChargeOrder;
using Ecommerce.Payment.Domain.Enums;
using Ecommerce.Payment.Infrastructure.Gateway;
using Ecommerce.Payment.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payment.Tests;

/// <summary>
/// What happens to the handler when its insert loses the race.
/// <para>
/// <c>Fifty_simultaneous_requests_record_exactly_one_payment</c> covers the same ground and covers
/// it <b>only when the race actually happens</b>. It passed on a pull request and failed on the
/// merge commit built from a byte-identical tree, because fifty concurrent requests usually resolve
/// through the read-first path and only occasionally have two reach the insert together. A test
/// that depends on winning a coin toss does not test the guarantee — it waits for it.
/// </para>
/// <para>
/// These force the losing path, so the behaviour is asserted rather than hoped for.
/// </para>
/// </summary>
[Collection(nameof(PaymentTestCollection))]
public class ConcurrentInsertRecoveryTests(PaymentTestFixture fixture)
{
    private readonly PaymentTestFixture _fixture = fixture;

    /// <summary>
    /// The regression test for the CI failure of 2026-09-19.
    /// <para>
    /// The handler reads first and finds nothing, builds its payment, and saves — into a unique
    /// constraint, because somebody else committed in between. Its catch is supposed to absorb that
    /// and reply with the row that won. It did not: a failed <c>SaveChangesAsync</c> leaves the rows
    /// it tried to write still tracked as <c>Added</c>, so the recovery path's save re-attempted the
    /// very insert that had just failed, raising the same violation outside the catch.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_losing_insert_still_replies_with_the_payment_that_won()
    {
        var orderId = Guid.CreateVersion7();

        // The winner commits first, from its own context.
        Guid winningPaymentId;
        await using (var winner = _fixture.NewScope())
        {
            var repository = winner.ServiceProvider.GetRequiredService<IPaymentRepository>();
            var payment = NewPayment(orderId);
            winningPaymentId = payment.Id;
            await repository.AddAsync(payment);
            await repository.SaveChangesAsync();
        }

        // The loser is blind for exactly one read — which is the race window, made deterministic.
        await using var provider = BuildBlindOnFirstReadProvider();
        await using var loser = provider.CreateAsyncScope();

        var result = await loser.ServiceProvider
            .GetRequiredService<ISender>()
            .Send(new ChargeOrderCommand(orderId, Guid.CreateVersion7(), 1_234m));

        // It answered rather than throwing. Throwing would leave the saga waiting forever.
        Assert.Equal(winningPaymentId, result.PaymentId);

        // And it did not write a second payment.
        await using var check = _fixture.NewScope();
        var (_, total) = await check.ServiceProvider
            .GetRequiredService<IPaymentRepository>()
            .GetPaginatedAsync(1, 100, orderId, null);

        Assert.Equal(1, total);
    }

    [Fact]
    public async Task A_second_save_after_a_unique_violation_would_retry_the_same_insert()
    {
        // The mechanism underneath, isolated: this is *why* the handler needed the discard, and it
        // fails without DiscardPendingChanges regardless of what any handler does.
        var orderId = Guid.CreateVersion7();

        await using var loser = _fixture.NewScope();
        var repository = loser.ServiceProvider.GetRequiredService<IPaymentRepository>();
        var unitOfWork = loser.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await repository.AddAsync(NewPayment(orderId));

        await using (var winner = _fixture.NewScope())
        {
            var winnerRepository = winner.ServiceProvider.GetRequiredService<IPaymentRepository>();
            await winnerRepository.AddAsync(NewPayment(orderId));
            await winnerRepository.SaveChangesAsync();
        }

        // The loser's save hits the unique constraint. That part is the guarantee working.
        var first = await Assert.ThrowsAnyAsync<Exception>(() => repository.SaveChangesAsync());
        Assert.Contains("23505", Describe(first));

        // Without discarding, the next save re-attempts the dead insert and raises it again.
        unitOfWork.DiscardPendingChanges();
        await repository.SaveChangesAsync();

        var (_, total) = await repository.GetPaginatedAsync(1, 100, orderId, null);
        Assert.Equal(1, total);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// The real stack, except that the first <c>GetByOrderIdAsync</c> reports nothing — which is
    /// precisely what a losing request sees when it reads a moment before the winner commits.
    /// </summary>
    private ServiceProvider BuildBlindOnFirstReadProvider()
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
        services.Configure<PaymentOutcomeOptions>(
            configuration.GetSection(PaymentOutcomeOptions.SectionName));
        services.AddSingleton<IPaymentGateway, StubPaymentGateway>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPaymentRepository>(sp =>
            new BlindOnFirstRead(new PaymentRepository(sp.GetRequiredService<PaymentDbContext>())));

        services.AddMassTransitTestHarness();

        services.AddAuditTrail("payment");

        return services.BuildServiceProvider(true);
    }

    private sealed class BlindOnFirstRead(IPaymentRepository inner) : IPaymentRepository
    {
        private bool _blinded;

        public Task<Domain.Entities.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        {
            if (_blinded)
            {
                return inner.GetByOrderIdAsync(orderId, ct);
            }

            _blinded = true;
            return Task.FromResult<Domain.Entities.Payment?>(null);
        }

        public Task<(List<Domain.Entities.Payment> Items, int TotalCount)> GetPaginatedAsync(
            int pageNumber, int pageSize, Guid? orderId, string? status, CancellationToken ct = default)
            => inner.GetPaginatedAsync(pageNumber, pageSize, orderId, status, ct);

        public Task AddAsync(Domain.Entities.Payment payment, CancellationToken ct = default)
            => inner.AddAsync(payment, ct);

        public Task<Domain.Entities.Refund?> GetRefundAsync(Guid orderId, CancellationToken ct = default)
            => inner.GetRefundAsync(orderId, ct);

        public Task<Dictionary<Guid, Domain.Entities.Refund>> GetRefundsAsync(
            IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
            => inner.GetRefundsAsync(orderIds, ct);

        public Task AddRefundAsync(Domain.Entities.Refund refund, CancellationToken ct = default)
            => inner.AddRefundAsync(refund, ct);

        public Task<Domain.Entities.Refund?> GetReturnRefundAsync(Guid returnId, CancellationToken ct = default)
            => inner.GetReturnRefundAsync(returnId, ct);

        public Task<decimal> GetRefundedTotalAsync(Guid orderId, CancellationToken ct = default)
            => inner.GetRefundedTotalAsync(orderId, ct);

        public Task SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    }

    private static Domain.Entities.Payment NewPayment(Guid orderId) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        UserId = Guid.CreateVersion7(),
        Amount = 1_234m,
        Status = PaymentStatus.Approved,
        Provider = "Stub",
        ProcessedAt = DateTime.UtcNow
    };

    private static string Describe(Exception exception)
    {
        var text = string.Empty;

        for (var current = exception; current is not null; current = current.InnerException)
        {
            text += current.Message;
        }

        return text;
    }
}
