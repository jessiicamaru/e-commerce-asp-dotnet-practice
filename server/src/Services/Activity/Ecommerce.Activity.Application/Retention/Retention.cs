using System.Globalization;
using Ecommerce.Shared.Audit;
using MediatR;

namespace Ecommerce.Activity.Application.Retention;

/// <summary>How long Activity keeps what it keeps (specs/116, #221). Checked when the service starts (specs/103).</summary>
public class RetentionOptions
{
    public const string Section = "Retention";

    /// <summary>A read notice goes this many days after it was read. Unread notices never go.</summary>
    public int ReadNotificationDays { get; set; } = 90;

    /// <summary>
    /// Audit entries older than this many years go - and each trim says so in the log. Unset (the default): the audit
    /// log is kept for ever (research D2).
    /// </summary>
    public int? AuditYears { get; set; }

    /// <summary>Rows per statement: short statements let the bell's reads interleave (research D3).</summary>
    public int BatchSize { get; set; } = 1000;

    public int IntervalMinutes { get; set; } = 60;

    /// <summary>What is out of range, by the setting's name; empty when all is well.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (ReadNotificationDays < 1) problems.Add($"{Section}:ReadNotificationDays must be 1 or more (was {ReadNotificationDays}).");
        if (AuditYears is < 1) problems.Add($"{Section}:AuditYears must be 1 or more, or unset to keep the audit log for ever (was {AuditYears}).");
        if (BatchSize < 1) problems.Add($"{Section}:BatchSize must be 1 or more (was {BatchSize}).");
        if (IntervalMinutes < 1) problems.Add($"{Section}:IntervalMinutes must be 1 or more (was {IntervalMinutes}).");
        return problems;
    }
}

public interface IRetentionRepository
{
    /// <summary>Deletes up to <paramref name="batch"/> notices read before <paramref name="cutoff"/>; how many went.</summary>
    Task<int> DeleteReadNotificationsAsync(DateTime cutoff, int batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes up to <paramref name="batch"/> audit entries that occurred before <paramref name="cutoff"/> and - when
    /// any went - runs <paramref name="stage"/> with how many, saving both in ONE transaction: the log never loses entries
    /// without saying so.
    /// </summary>
    Task<int> TrimAuditEntriesAsync(DateTime cutoff, int batch, Func<int, CancellationToken, Task> stage, CancellationToken cancellationToken = default);
}

public record RetentionResult(int Notifications, int AuditEntries);

/// <summary>One sweep (specs/116): read notices past their days, and audit entries past their years when an operator set them.</summary>
public record ApplyRetentionCommand(DateTime Now) : IRequest<RetentionResult>;

public class ApplyRetentionCommandHandler(IRetentionRepository retention, RetentionOptions options, IAuditTrail audit)
    : IRequestHandler<ApplyRetentionCommand, RetentionResult>
{
    private readonly IRetentionRepository _retention = retention;
    private readonly RetentionOptions _options = options;
    private readonly IAuditTrail _audit = audit;

    public async Task<RetentionResult> Handle(ApplyRetentionCommand request, CancellationToken cancellationToken)
    {
        var notices = 0;
        var noticeCutoff = request.Now.AddDays(-_options.ReadNotificationDays);
        int deleted;
        do
        {
            deleted = await _retention.DeleteReadNotificationsAsync(noticeCutoff, _options.BatchSize, cancellationToken);
            notices += deleted;
        }
        while (deleted == _options.BatchSize);

        var entries = 0;
        if (_options.AuditYears is { } years)
        {
            var auditCutoff = request.Now.AddYears(-years);
            do
            {
                deleted = await _retention.TrimAuditEntriesAsync(auditCutoff, _options.BatchSize,
                    (removed, ct) => _audit.RecordAsync(
                        AuditCategory.System, "AuditTrimmed", "AuditLog", null,
                        $"Audit entries before {auditCutoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} removed: {removed}",
                        after: new { cutoff = auditCutoff, removed, years }, cancellationToken: ct),
                    cancellationToken);
                entries += deleted;
            }
            while (deleted == _options.BatchSize);
        }

        return new RetentionResult(notices, entries);
    }
}
