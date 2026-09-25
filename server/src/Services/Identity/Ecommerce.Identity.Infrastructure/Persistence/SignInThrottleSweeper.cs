using Ecommerce.Application.Auth.SignInThrottling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Infrastructure.Persistence;

/// <summary>
/// Deletes wrong-password counts nobody needs any more (specs/062): no pause running and a window long over.
/// Without it every address somebody tried - most of them with no account - would stay a row for ever.
/// </summary>
/// <remarks>The shape of the other sweepers: a periodic timer, a scope per tick, and a bad tick never ends it.</remarks>
public class SignInThrottleSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    TimeSpan interval,
    ILogger<SignInThrottleSweeper> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly TimeProvider _clock = clock;
    private readonly TimeSpan _interval = interval;
    private readonly ILogger<SignInThrottleSweeper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var purged = await scope.ServiceProvider.GetRequiredService<ISignInThrottle>()
                    .PurgeStaleAsync(_clock.GetUtcNow().UtcDateTime, stoppingToken);

                if (purged > 0)
                {
                    _logger.LogInformation("Purged {Count} stale sign-in count(s).", purged);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Purging sign-in counts failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
