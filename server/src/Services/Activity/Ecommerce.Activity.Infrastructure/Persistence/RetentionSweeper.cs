using Ecommerce.Activity.Application.Retention;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Activity.Infrastructure.Persistence;

/// <summary>
/// Applies retention (specs/116, #221): read notices past their days, audit entries past their years when an operator set
/// them. The shape of the other sweepers - a periodic timer, a scope per tick, a bad tick never ends it - and it runs once
/// at start, so a restart applies a changed setting at once.
/// </summary>
public class RetentionSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    TimeSpan interval,
    ILogger<RetentionSweeper> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly TimeProvider _clock = clock;
    private readonly TimeSpan _interval = interval;
    private readonly ILogger<RetentionSweeper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new ApplyRetentionCommand(_clock.GetUtcNow().UtcDateTime), stoppingToken);

                if (result.Notifications > 0 || result.AuditEntries > 0)
                {
                    _logger.LogInformation("Retention removed {Notifications} read notice(s) and {AuditEntries} audit entr(ies).",
                        result.Notifications, result.AuditEntries);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Applying retention failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
