using Ecommerce.Orchestrator.WebApi.StateMachines;
using MassTransit;

namespace Ecommerce.Orchestrator.WebApi.Timeouts;

/// <summary>
/// Stops the saga waiting for a Payment that has not answered (specs/053, #123).
/// </summary>
/// <remarks>
/// <para>
/// The shape of Inventory's <c>ReservationExpirySweeper</c> and Order's <c>DeliveryConfirmationSweeper</c>:
/// a periodic timer, a scope per tick, and one bad tick never ends it.
/// </para>
/// <para>
/// A sweeper and not MassTransit's <c>Schedule</c>: durable scheduling needs RabbitMQ's delayed-message
/// plugin or a Quartz store, and neither exists in compose or CI; the in-memory scheduler forgets every
/// pending timeout on restart - exactly when a stuck payment is likeliest. The saga table already knows
/// who is waiting and since when.
/// </para>
/// <para>
/// Safe on several instances: two sweepers may announce the same order, and the saga handles the second
/// as nothing.
/// </para>
/// </remarks>
public class PaymentTimeoutSweeper(
    IServiceScopeFactory scopeFactory,
    PaymentTimeoutOptions options,
    TimeProvider clock,
    ILogger<PaymentTimeoutSweeper> logger
) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly PaymentTimeoutOptions _options = options;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<PaymentTimeoutSweeper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Payment timeout sweeper started: an order waits {Timeout} for Payment, checked every {Interval}.",
            _options.Timeout, _options.SweepInterval);

        using var timer = new PeriodicTimer(_options.SweepInterval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cutoff = _clock.GetUtcNow().UtcDateTime - _options.Timeout;
                var expired = await PaymentTimeouts.ExpireAsync(
                    scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(),
                    cutoff,
                    stoppingToken);

                if (expired > 0)
                {
                    _logger.LogWarning("{Count} order(s) waited longer than {Timeout} for Payment and will fail.", expired, _options.Timeout);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Payment timeout sweep failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
