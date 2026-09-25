using Ecommerce.Application.Email;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Infrastructure.Email;

/// <summary>
/// Sends the emails that are due (specs/060), every <c>Email:SweepSeconds</c> (15 by default).
/// </summary>
/// <remarks>
/// The shape of Inventory's expiry sweeper and the saga's payment-timeout sweeper: a periodic timer, a scope
/// per tick, and one bad tick - a mail server that is down, a database that blinked - never ends it. Safe on
/// several instances: the claim skips rows another instance holds.
/// </remarks>
public class EmailDispatchSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    TimeSpan interval,
    ILogger<EmailDispatchSweeper> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly TimeProvider _clock = clock;
    private readonly TimeSpan _interval = interval;
    private readonly ILogger<EmailDispatchSweeper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email dispatcher started: due emails are sent every {Interval}.", _interval);
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new DispatchEmailsCommand(_clock.GetUtcNow().UtcDateTime), stoppingToken);

                if (sent > 0)
                {
                    _logger.LogInformation("Sent {Count} email(s).", sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Email dispatch failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
