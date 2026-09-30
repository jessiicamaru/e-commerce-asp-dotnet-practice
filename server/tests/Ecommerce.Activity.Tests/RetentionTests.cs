using Ecommerce.Activity.Application.Retention;
using Ecommerce.Activity.Domain.Entities;
using Ecommerce.Activity.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// Retention (specs/116, #221) against the real PostgreSQL: read notices go after their days and unread ones never; the
/// audit log is kept for ever unless an operator sets years, and then every trim says so in the log; batches carry on
/// until nothing is left, and two sweeps at once remove each row once.
/// </summary>
/// <remarks>Other tests share these tables, so every row here is years or months old - old enough that nothing another
/// test made is ever in the way - and each assertion is about this test's own rows.</remarks>
[Collection(nameof(ActivityTestCollection))]
public class RetentionTests : IDisposable
{
    private readonly ActivityTestFixture _fixture;
    private readonly DateTime _now = DateTime.UtcNow;

    public RetentionTests(ActivityTestFixture fixture)
    {
        _fixture = fixture;
        Defaults();
    }

    public void Dispose()
    {
        Defaults();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Read_notices_past_their_days_go_and_recent_or_unread_ones_stay()
    {
        var old = await NoticeAsync(readDaysAgo: 91);
        var recent = await NoticeAsync(readDaysAgo: 89);
        var unread = await NoticeAsync(readDaysAgo: null, createdDaysAgo: 400);

        await SweepAsync();

        Assert.Equal([recent, unread], (await NoticesLeftAsync(old, recent, unread)).Order());
    }

    [Fact]
    public async Task Batches_carry_on_until_nothing_is_left()
    {
        _fixture.Retention.BatchSize = 2;
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++) ids.Add(await NoticeAsync(readDaysAgo: 200));

        await SweepAsync();

        Assert.Empty(await NoticesLeftAsync([.. ids]));
    }

    [Fact]
    public async Task Two_sweeps_at_once_remove_each_row_once_and_neither_fails()
    {
        _fixture.Retention.BatchSize = 2;
        var ids = new List<Guid>();
        for (var i = 0; i < 6; i++) ids.Add(await NoticeAsync(readDaysAgo: 300));

        await Task.WhenAll(SweepAsync(), SweepAsync());

        Assert.Empty(await NoticesLeftAsync([.. ids]));
    }

    [Fact]
    public async Task The_audit_log_is_kept_for_ever_unless_an_operator_sets_years()
    {
        var ancient = await EntryAsync(yearsAgo: 30);

        var result = await SweepAsync();

        Assert.Equal(0, result.AuditEntries);
        Assert.Equal([ancient], await EntriesLeftAsync(ancient));
    }

    [Fact]
    public async Task With_years_set_older_entries_go_and_each_trim_says_so_in_the_log()
    {
        _fixture.Retention.AuditYears = 5;
        var old = new[] { await EntryAsync(yearsAgo: 6), await EntryAsync(yearsAgo: 7), await EntryAsync(yearsAgo: 8) };
        var kept = await EntryAsync(yearsAgo: 4);
        var trimsBefore = Trims().Count;

        var result = await SweepAsync();

        Assert.Equal([kept], await EntriesLeftAsync([.. old, kept]));
        var trims = Trims().Skip(trimsBefore).ToList();
        Assert.NotEmpty(trims);
        Assert.Equal(result.AuditEntries, trims.Sum(t => int.Parse(t.Summary[(t.Summary.LastIndexOf(' ') + 1)..])));
        Assert.All(trims, t => Assert.Equal(("System", "AuditTrimmed", "AuditLog"), (t.Category, t.Action, t.SubjectType)));

        var trimsNow = Trims().Count;
        await SweepAsync();   // nothing left to remove: nothing recorded
        Assert.Equal(trimsNow, Trims().Count);
    }

    [Theory]
    [InlineData(0, null, 1000, 60, "ReadNotificationDays")]
    [InlineData(90, 0, 1000, 60, "AuditYears")]
    [InlineData(90, null, 0, 60, "BatchSize")]
    [InlineData(90, null, 1000, 0, "IntervalMinutes")]
    public void A_setting_out_of_range_is_named(int days, int? years, int batch, int minutes, string named)
    {
        var options = new RetentionOptions { ReadNotificationDays = days, AuditYears = years, BatchSize = batch, IntervalMinutes = minutes };

        Assert.Contains(options.Problems(), p => p.Contains(named));
        Assert.Empty(new RetentionOptions().Problems());
        Assert.Empty(new RetentionOptions { AuditYears = 1, ReadNotificationDays = 1 }.Problems());
    }

    // ------------------------------------------------------------------ helpers

    private void Defaults()
    {
        _fixture.Retention.ReadNotificationDays = 90;
        _fixture.Retention.AuditYears = null;
        _fixture.Retention.BatchSize = 1000;
        _fixture.Retention.IntervalMinutes = 60;
    }

    private async Task<RetentionResult> SweepAsync()
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyRetentionCommand(_now));
    }

    private List<AuditEntryRecorded> Trims() =>
        _fixture.Services.GetRequiredService<ITestHarness>().Published.Select<AuditEntryRecorded>()
            .Select(x => x.Context.Message).Where(e => e.Action == "AuditTrimmed").ToList();

    private async Task<Guid> NoticeAsync(int? readDaysAgo, int createdDaysAgo = 500)
    {
        var notice = new Notification
        {
            Id = Guid.CreateVersion7(), RecipientId = Guid.CreateVersion7(), Kind = "OrderPaid", Data = "{}",
            CreatedAt = _now.AddDays(-createdDaysAgo), ReadAt = readDaysAgo is { } days ? _now.AddDays(-days) : null,
        };
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        db.Notifications.Add(notice);
        await db.SaveChangesAsync();
        return notice.Id;
    }

    private async Task<Guid> EntryAsync(int yearsAgo)
    {
        var entry = new AuditEntry
        {
            Id = Guid.CreateVersion7(), Category = "User", Action = "Changed", SubjectType = "User", Summary = "Retention test",
            Changes = "[]", Service = "identity", OccurredAt = _now.AddYears(-yearsAgo).AddDays(-1), RecordedAt = _now,
        };
        await using var scope = _fixture.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private async Task<List<Guid>> NoticesLeftAsync(params Guid[] ids)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ActivityDbContext>().Notifications.Where(n => ids.Contains(n.Id)).Select(n => n.Id).ToListAsync();
    }

    private async Task<List<Guid>> EntriesLeftAsync(params Guid[] ids)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ActivityDbContext>().AuditEntries.Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync();
    }
}
