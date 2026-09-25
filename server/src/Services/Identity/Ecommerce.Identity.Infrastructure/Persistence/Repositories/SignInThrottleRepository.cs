using Ecommerce.Application.Auth.SignInThrottling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public class SignInThrottleRepository(ApplicationDbContext context, IOptions<SignInOptions> options) : ISignInThrottle
{
    private readonly ApplicationDbContext _context = context;
    private readonly SignInOptions _options = options.Value;

    public async Task<DateTime?> BlockedUntilAsync(string emailKey, DateTime now, CancellationToken cancellationToken = default)
    {
        var until = await _context.Database.SqlQuery<DateTime>($"""
            SELECT "BlockedUntil" AS "Value" FROM sign_in_throttles
            WHERE "EmailKey" = {emailKey} AND "BlockedUntil" > {now}
            """).ToListAsync(cancellationToken);

        return until.Count == 1 ? until[0] : null;
    }

    public async Task<FailureOutcome> RecordFailureAsync(string emailKey, DateTime now, CancellationToken cancellationToken = default)
    {
        // To PostgreSQL's precision first: it keeps microseconds, .NET keeps tenths of them, and the instant
        // written must compare equal to the instant read back below.
        now = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        var windowOpenedAfter = now - _options.Window;
        var pauseUntil = now + _options.Cooldown;
        var max = _options.MaxFailures;

        // ONE statement, so simultaneous wrong passwords are each counted and exactly one of them starts the
        // pause. n = the count after this failure: 1 when the window is over, else one more. Reaching the
        // limit starts a pause and puts the count back to 0, so after the pause another full run of wrong
        // passwords is needed before the next one.
        var rows = await _context.Database.SqlQuery<ThrottleRow>($"""
            INSERT INTO sign_in_throttles ("EmailKey", "Failures", "WindowStartedAt", "BlockedUntil")
            VALUES ({emailKey},
                    CASE WHEN 1 >= {max} THEN 0 ELSE 1 END,
                    {now},
                    CASE WHEN 1 >= {max} THEN {pauseUntil} ELSE NULL END)
            ON CONFLICT ("EmailKey") DO UPDATE SET
                "Failures" = CASE
                    WHEN (CASE WHEN sign_in_throttles."WindowStartedAt" <= {windowOpenedAfter} THEN 1
                               ELSE sign_in_throttles."Failures" + 1 END) >= {max} THEN 0
                    ELSE (CASE WHEN sign_in_throttles."WindowStartedAt" <= {windowOpenedAfter} THEN 1
                               ELSE sign_in_throttles."Failures" + 1 END) END,
                "BlockedUntil" = CASE
                    WHEN (CASE WHEN sign_in_throttles."WindowStartedAt" <= {windowOpenedAfter} THEN 1
                               ELSE sign_in_throttles."Failures" + 1 END) >= {max} THEN {pauseUntil}
                    ELSE sign_in_throttles."BlockedUntil" END,
                "WindowStartedAt" = CASE
                    WHEN sign_in_throttles."WindowStartedAt" <= {windowOpenedAfter}
                      OR sign_in_throttles."Failures" + 1 >= {max} THEN {now}
                    ELSE sign_in_throttles."WindowStartedAt" END
            RETURNING "Failures", "BlockedUntil"
            """).ToListAsync(cancellationToken);

        var row = rows.Single();
        // Only the statement that started the pause wrote this exact instant with the count back at 0.
        var paused = row.Failures == 0 && row.BlockedUntil == pauseUntil;
        return new FailureOutcome(paused, row.BlockedUntil);
    }

    public Task ClearAsync(string emailKey, CancellationToken cancellationToken = default) =>
        _context.Database.ExecuteSqlAsync(
            $"""DELETE FROM sign_in_throttles WHERE "EmailKey" = {emailKey}""", cancellationToken);

    public Task<int> PurgeStaleAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var windowOpenedAfter = now - _options.Window;
        return _context.Database.ExecuteSqlAsync($"""
            DELETE FROM sign_in_throttles
            WHERE ("BlockedUntil" IS NULL OR "BlockedUntil" <= {now})
              AND "WindowStartedAt" <= {windowOpenedAfter}
            """, cancellationToken);
    }

    private sealed class ThrottleRow
    {
        public int Failures { get; set; }

        public DateTime? BlockedUntil { get; set; }
    }
}
