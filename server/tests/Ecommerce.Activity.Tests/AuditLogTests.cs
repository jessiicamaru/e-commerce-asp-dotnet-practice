using Ecommerce.Activity.Application.Audit.Commands;
using Ecommerce.Activity.Application.Audit.Queries;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>Keeping and reading the log (specs/041).</summary>
[Collection(nameof(ActivityTestCollection))]
public class AuditLogTests(ActivityTestFixture fixture)
{
    private readonly ActivityTestFixture _fixture = fixture;

    [Fact]
    public async Task An_entry_is_kept_with_its_diff()
    {
        var entry = Entry("Catalog", "ProductUpdated", subjectId: Guid.NewGuid().ToString(),
            before: """{"name":"Old","price":1}""", after: """{"name":"New","price":1}""");

        Assert.True(await SendAsync(new RecordAuditEntryCommand(entry)));

        var read = await SendAsync(new GetAuditEntryQuery(entry.EntryId));
        Assert.Equal("ProductUpdated", read.Action);
        var change = Assert.Single(read.Changes);
        Assert.Equal("name", change.Path);
        Assert.Equal("New", read.After!.Value.GetProperty("name").GetString());
    }

    /// <summary>FR-002: a message delivered twice - or twice at once - is one entry.</summary>
    [Fact]
    public async Task A_redelivered_entry_is_kept_once()
    {
        var entry = Entry("Payment", "PayoutRecorded", actor: $"{Guid.NewGuid():N}@demo.test");

        var kept = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(new RecordAuditEntryCommand(entry))));

        Assert.Equal(1, kept.Count(k => k));
        var page = await SendAsync(new GetAuditEntriesQuery(Actor: entry.ActorEmail));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task The_log_filters_by_category_actor_subject_and_period()
    {
        var who = $"{Guid.NewGuid():N}@demo.test";
        var subject = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await SendAsync(new RecordAuditEntryCommand(Entry("Order", "OrderPlaced", who, subject, at: now.AddHours(-2))));
        await SendAsync(new RecordAuditEntryCommand(Entry("Order", "OrderCancelled", who, subject, at: now.AddHours(-1))));
        await SendAsync(new RecordAuditEntryCommand(Entry("Security", "SignedIn", who, at: now)));

        var orders = await SendAsync(new GetAuditEntriesQuery(Category: "Order", Actor: who[..10].ToUpperInvariant()));
        Assert.Equal(["OrderCancelled", "OrderPlaced"], orders.Items.Select(i => i.Action));   // newest first

        var onSubject = await SendAsync(new GetAuditEntriesQuery(SubjectType: "Thing", SubjectId: subject));
        Assert.Equal(2, onSubject.TotalCount);

        var lastHour = await SendAsync(new GetAuditEntriesQuery(Actor: who, From: now.AddMinutes(-90)));
        Assert.Equal(["SignedIn", "OrderCancelled"], lastHour.Items.Select(i => i.Action));
    }

    [Fact]
    public async Task The_summary_counts_each_category()
    {
        var from = DateTime.UtcNow.AddYears(-10).AddDays(-1);
        var to = DateTime.UtcNow.AddYears(-10);
        await SendAsync(new RecordAuditEntryCommand(Entry("Moderation", "ShopApproved", at: to.AddHours(-1))));
        await SendAsync(new RecordAuditEntryCommand(Entry("Moderation", "ShopRejected", at: to.AddHours(-2))));
        await SendAsync(new RecordAuditEntryCommand(Entry("System", "DeliveriesAutoConfirmed", at: to.AddHours(-3))));

        var counts = (await SendAsync(new GetAuditSummaryQuery(from, to))).ToDictionary(c => c.Category, c => c.Count);

        Assert.Equal(2, counts["Moderation"]);
        Assert.Equal(1, counts["System"]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new GetAuditEntryQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task An_unknown_category_is_refused()
    {
        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(new GetAuditEntriesQuery(Category: "Gossip")));
    }

    // ------------------------------------------------------------------ helpers

    private static AuditEntryRecorded Entry(
        string category, string action, string? actor = null, string? subjectId = null,
        string? before = null, string? after = null, DateTime? at = null) =>
        new(Guid.CreateVersion7(), category, action, actor is null ? null : Guid.NewGuid(), actor, "Admin",
            "Thing", subjectId, $"{action} happened", before, after, "tests", at ?? DateTime.UtcNow);

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
