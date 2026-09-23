using Ecommerce.Order.Application.Orders.Commands.ConfirmDelivery;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Order.Infrastructure.Delivery;

/// <summary>
/// Takes a parcel nobody confirmed as delivered once <see cref="DeliveryOptions.AutoConfirmDays"/> have
/// passed since it shipped (specs/040 US3), so a seller is not left unpaid by a customer who never clicks.
/// </summary>
/// <remarks>
/// The shape of Inventory's <c>ReservationExpirySweeper</c>: a periodic timer, a scope per tick, one command.
/// Safe on several instances - the command is one guarded statement (research D3).
/// </remarks>
public class DeliveryConfirmationSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<DeliveryOptions> options,
    TimeProvider clock,
    ILogger<DeliveryConfirmationSweeper> logger
) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly DeliveryOptions _options = options.Value;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<DeliveryConfirmationSweeper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.SweepIntervalMinutes);
        _logger.LogInformation(
            "Delivery confirmation sweeper started: parcels are taken as delivered {Days} day(s) after shipping, checked every {Interval}.",
            _options.AutoConfirmDays, interval);

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-_options.AutoConfirmDays);
                await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new AutoConfirmDeliveriesCommand(cutoff), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // One bad sweep must not kill the sweeper: the next tick tries again.
                _logger.LogError(exception, "Delivery confirmation sweep failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
