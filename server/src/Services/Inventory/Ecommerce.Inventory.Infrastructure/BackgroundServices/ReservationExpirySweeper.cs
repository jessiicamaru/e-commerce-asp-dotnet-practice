using Ecommerce.Inventory.Application.Reservations.ExpireStock;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Inventory.Infrastructure.BackgroundServices;

/// <summary>
/// Returns stock held by orders that never settled. Without it, a saga that dies mid-flight
/// removes sellable stock permanently and the only remedy is editing the database by hand.
/// </summary>
public class ReservationExpirySweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationExpiryOptions> options,
    ILogger<ReservationExpirySweeper> logger
) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ReservationExpiryOptions _options = options.Value;
    private readonly ILogger<ReservationExpirySweeper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.SweepIntervalSeconds));

        _logger.LogInformation(
            "Reservation expiry sweeper started: holding period {Ttl} minute(s), sweeping every {Interval}.",
            _options.ReservationTtlMinutes,
            interval);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                await mediator.Send(new ExpireStockCommand(_options.BatchSize), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // One bad sweep must not kill the sweeper: the next tick tries again.
                _logger.LogError(exception, "Reservation expiry sweep failed; will retry next tick.");
            }
        }
    }
}
