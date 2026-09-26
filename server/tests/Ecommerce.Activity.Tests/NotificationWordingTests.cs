using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Application.Notifications;
using Ecommerce.Activity.Domain.Entities;
using Ecommerce.Activity.Infrastructure.Persistence;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>
/// An administrator rewords the notifications (specs/078, #150): what they save is what the storefront is given, in
/// that language; a placeholder the kind cannot fill is refused by name; what could run in a bell is stripped and a
/// link stays on the web or the shop; two saves at once make one version; and every version can be undone.
/// </summary>
/// <remarks>The wording is global to the database, so every test starts and ends with none saved.</remarks>
[Collection(nameof(ActivityTestCollection))]
public class NotificationWordingTests(ActivityTestFixture fixture) : IAsyncLifetime
{
    private readonly ActivityTestFixture _fixture = fixture;

    public async Task InitializeAsync()
    {
        _fixture.CurrentUser.Id = Guid.CreateVersion7();
        await ForgetAsync();
    }

    public Task DisposeAsync() => ForgetAsync();

    [Fact]
    public async Task What_an_administrator_saves_is_what_the_storefront_is_given_in_that_language_only()
    {
        Assert.Empty((await SendAsync(new GetNotificationWordingQuery()))["en"]);

        var saved = await SendAsync(new SaveNotificationWordingCommand("OrderPaid", "en",
            "Order <strong>{{order}}</strong> is paid: {{total}}. <a href=\"/orders\">Your orders</a>", 0));

        var current = await SendAsync(new GetNotificationWordingQuery());
        Assert.Equal((1, false), (saved.Version, saved.IsDefault));
        Assert.Equal("Order <strong>{{order}}</strong> is paid: {{total}}. <a href=\"/orders\">Your orders</a>", current["en"]["OrderPaid"]);
        Assert.Empty(current["vi"]);
    }

    /// <summary>The issue's acceptance: refused, with a message that names the placeholder.</summary>
    [Fact]
    public async Task A_placeholder_the_kind_cannot_fill_is_refused_by_name()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new SaveNotificationWordingCommand("ProductApproved", "en", "“{{product}}” for {{total}} is on sale", 0)));

        var message = Assert.Single(refused.Errors).ErrorMessage;
        Assert.StartsWith("{{total}} is not something a ProductApproved notice can fill in", message);
        Assert.Contains("{{product}}", message);
    }

    /// <summary>The storefront fills `{{count}}` from a review's rating, so NewReview's plural forms may use it.</summary>
    [Fact]
    public async Task A_plural_form_is_a_key_of_its_kind_with_that_kind_s_placeholders()
    {
        var one = await SendAsync(new SaveNotificationWordingCommand("NewReview_one", "en", "{{count}} star for “{{product}}”", 0));

        Assert.Equal("NewReview_one", one.Key);
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new SaveNotificationWordingCommand("NewReview_lots", "en", "x", 0)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new SaveNotificationWordingCommand("SomethingNew", "en", "x", 0)));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new SaveNotificationWordingCommand("OrderPaid", "fr", "x", 0)));
    }

    [Fact]
    public async Task What_could_run_in_a_bell_is_stripped_and_a_link_stays_on_the_web_or_the_shop()
    {
        var saved = await SendAsync(new SaveNotificationWordingCommand("OrderPaid", "en",
            "<p onclick=\"steal()\">Paid <em>{{order}}</em><script>alert(1)</script><img src=x onerror=y>"
            + "<a href=\"javascript:alert(1)\">x</a><h1>big</h1></p>", 0));

        Assert.Equal("Paid <em>{{order}}</em><a>x</a>big", saved.Text);

        var refused = await Assert.ThrowsAsync<ValidationException>(() =>
            SendAsync(new SaveNotificationWordingCommand("OrderPaid", "en", "Paid <a href=\"//evil.test/x\">here</a>", 1)));
        Assert.Contains("//evil.test/x", Assert.Single(refused.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_stale_editor_is_a_409_and_the_store_gives_a_version_number_once()
    {
        await SendAsync(new SaveNotificationWordingCommand("NewSale", "vi", "Đơn mới {{order}}", 0));

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new SaveNotificationWordingCommand("NewSale", "vi", "Cũ", 0)));
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new SaveNotificationWordingCommand("NewSale", "vi", "Tương lai", 7)));

        var staged = 0;
        Assert.True(await AddAsync(2, () => staged++));
        Assert.False(await AddAsync(2, () => staged++));
        Assert.Equal(1, staged);
    }

    [Fact]
    public async Task Reset_and_restore_are_new_versions_and_each_is_audited_with_before_and_after()
    {
        await SendAsync(new SaveNotificationWordingCommand("ParcelShipped", "en", "On its way: {{tracking}}", 0));
        var (reset, audit) = await AuditedAsync(new ResetNotificationWordingCommand("ParcelShipped", "en", 1));
        Assert.False((await SendAsync(new GetNotificationWordingQuery()))["en"].ContainsKey("ParcelShipped"));   // the bundle again
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new ResetNotificationWordingCommand("ParcelShipped", "en", 2)));
        var restored = await SendAsync(new RestoreNotificationWordingCommand("ParcelShipped", "en", 1, 2));

        Assert.Equal((2, true, (string?)null), (reset.Version, reset.IsDefault, reset.Text));
        Assert.Equal((3, "On its way: {{tracking}}"), (restored.Version, restored.Text));
        var entry = Assert.Single(audit, a => a.Action == "NotificationWordingReset");
        Assert.Equal(("System", "ParcelShipped/en"), (entry.Category, entry.SubjectId));
        Assert.Contains("On its way", entry.Before);
        Assert.Equal([3, 2, 1], (await SendAsync(new GetNotificationWordingVersionsQuery("ParcelShipped", "en"))).Select(v => v.Version));
        Assert.Equal("On its way: {{tracking}}", (await SendAsync(new GetNotificationWordingQuery()))["en"]["ParcelShipped"]);
    }

    [Fact]
    public async Task The_overview_lists_every_kind_with_what_its_words_may_use()
    {
        await SendAsync(new SaveNotificationWordingCommand("OrderCancelled", "vi", "Đơn {{order}} đã huỷ {{by}}", 0));

        var overview = await SendAsync(new GetNotificationWordingOverviewQuery());

        Assert.Equal(NotificationContract.Kinds.Count, overview.Kinds.Count);
        Assert.Equal(["order", "total"], overview.Kinds.Single(k => k.Kind == "OrderPaid").Placeholders);
        Assert.Equal(["count", "product", "rating"], overview.Kinds.Single(k => k.Kind == "NewReview").Placeholders);
        // An optional key fills a placeholder too: a parcel names its shop when it has one, and the storefront words
        // the shop's own otherwise.
        Assert.Equal(["order", "shop", "tracking"], overview.Kinds.Single(k => k.Kind == "ParcelShipped").Placeholders);
        Assert.Empty(overview.Kinds.Single(k => k.Kind == "ModeratorGranted").Placeholders);
        Assert.Equal("OrderCancelled", Assert.Single(overview.Entries).Key);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<bool> AddAsync(int version, Action onStage)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<INotificationWordingStore>().TryAddAsync(new NotificationWordingVersion
        {
            Key = "NewSale",
            Language = "vi",
            Version = version,
            Text = "x",
            CreatedBy = Guid.CreateVersion7(),
        }, _ =>
        {
            onStage();
            return Task.CompletedTask;
        });
    }

    private async Task<(T Result, List<AuditEntryRecorded> Audit)> AuditedAsync<T>(IRequest<T> request)
    {
        var harness = _fixture.Services.GetRequiredService<ITestHarness>();
        var before = harness.Published.Select<AuditEntryRecorded>().Count();
        var result = await SendAsync(request);
        return (result, harness.Published.Select<AuditEntryRecorded>().Skip(before).Select(x => x.Context.Message).ToList());
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task ForgetAsync()
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ActivityDbContext>().NotificationWordingVersions.ExecuteDeleteAsync();
    }
}
