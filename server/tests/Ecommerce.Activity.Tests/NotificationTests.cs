using Ecommerce.Activity.Application.Notifications;
using Ecommerce.Contracts.Activity;
using Ecommerce.Shared.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Activity.Tests;

/// <summary>Inboxes (specs/042): one notification per id, each person's own, read and marked by them alone.</summary>
[Collection(nameof(ActivityTestCollection))]
public class NotificationTests(ActivityTestFixture fixture)
{
    private readonly ActivityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_notification_lands_in_its_recipient_s_inbox_once()
    {
        var lan = Guid.NewGuid();
        var notice = Notice(lan, "OrderPaid", ("orderId", "o-1"), ("total", "22462000"));

        var kept = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(new RecordNotificationCommand(notice))));

        Assert.Equal(1, kept.Count(k => k));
        var inbox = await As(lan, () => SendAsync(new GetMyNotificationsQuery()));
        var only = Assert.Single(inbox.Items);
        Assert.Equal("OrderPaid", only.Kind);
        Assert.Equal("22462000", only.Data["total"]);
        Assert.Equal("/orders/o-1", only.Link);
        Assert.Null(only.ReadAt);
    }

    /// <summary>FR-003: nobody reads or marks another person's notifications - and cannot tell they exist.</summary>
    [Fact]
    public async Task Nobody_reads_or_marks_another_s_notifications()
    {
        var lan = Guid.NewGuid();
        var minh = Guid.NewGuid();
        var notice = Notice(lan, "ParcelShipped");
        await SendAsync(new RecordNotificationCommand(notice));

        Assert.Empty((await As(minh, () => SendAsync(new GetMyNotificationsQuery()))).Items);
        Assert.Equal(0, await As(minh, () => SendAsync(new GetMyUnreadCountQuery())));

        var theirs = await Assert.ThrowsAsync<NotFoundException>(() => As(minh, () => SendAsync(new MarkNotificationReadCommand(notice.NotificationId))));
        var nothing = await Assert.ThrowsAsync<NotFoundException>(() => As(minh, () => SendAsync(new MarkNotificationReadCommand(Guid.NewGuid()))));
        Assert.Equal(nothing.Message, theirs.Message);
        Assert.Equal(1, await As(lan, () => SendAsync(new GetMyUnreadCountQuery())));
    }

    [Fact]
    public async Task Marking_read_lowers_the_count_and_all_at_once_clears_it()
    {
        var lan = Guid.NewGuid();
        var first = Notice(lan, "OrderPaid");
        await SendAsync(new RecordNotificationCommand(first));
        await SendAsync(new RecordNotificationCommand(Notice(lan, "ParcelShipped")));
        await SendAsync(new RecordNotificationCommand(Notice(lan, "OrderCancelled")));
        Assert.Equal(3, await As(lan, () => SendAsync(new GetMyUnreadCountQuery())));

        await As(lan, () => SendAsync(new MarkNotificationReadCommand(first.NotificationId)));
        await As(lan, () => SendAsync(new MarkNotificationReadCommand(first.NotificationId)));   // again: fine
        Assert.Equal(2, await As(lan, () => SendAsync(new GetMyUnreadCountQuery())));
        Assert.Equal(2, (await As(lan, () => SendAsync(new GetMyNotificationsQuery(UnreadOnly: true)))).TotalCount);

        Assert.Equal(2, await As(lan, () => SendAsync(new MarkAllNotificationsReadCommand())));
        Assert.Equal(0, await As(lan, () => SendAsync(new GetMyUnreadCountQuery())));
    }

    [Fact]
    public async Task The_inbox_is_newest_first_a_page_at_a_time()
    {
        var lan = Guid.NewGuid();
        var start = DateTime.UtcNow.AddMinutes(-10);
        for (var i = 0; i < 5; i++)
        {
            await SendAsync(new RecordNotificationCommand(Notice(lan, $"Kind{i}", at: start.AddMinutes(i))));
        }

        var page = await As(lan, () => SendAsync(new GetMyNotificationsQuery(Page: 1, PageSize: 2)));

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["Kind4", "Kind3"], page.Items.Select(i => i.Kind));
    }

    private static UserNotificationRequested Notice(Guid to, string kind, params (string, string)[] data) =>
        Notice(to, kind, null, data);

    private static UserNotificationRequested Notice(Guid to, string kind, DateTime? at, params (string Key, string Value)[] data) =>
        new(Guid.CreateVersion7(), to, kind, data.ToDictionary(d => d.Key, d => d.Value), "/orders/o-1", at ?? DateTime.UtcNow);

    private async Task<T> As<T>(Guid user, Func<Task<T>> body)
    {
        _fixture.CurrentUser.Id = user;
        return await body();
    }

    private async Task As(Guid user, Func<Task> body)
    {
        _fixture.CurrentUser.Id = user;
        await body();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _fixture.NewScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        await using var scope = _fixture.NewScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
