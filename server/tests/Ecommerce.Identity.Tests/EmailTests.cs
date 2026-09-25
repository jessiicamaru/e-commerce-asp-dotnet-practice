using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Email;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Email;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// Email (specs/060): a request is kept once, sent once, in its language - and a mail server that is down
/// delays it rather than losing it.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class EmailTests(IdentityTestFixture fixture)
{
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_request_delivered_twice_is_kept_once_and_sent_once()
    {
        var (id, email) = await PersonAsync();
        var request = Request(id, "vi");

        Assert.True(await SendAsync(new QueueEmailCommand(request)));
        Assert.False(await SendAsync(new QueueEmailCommand(request)));   // redelivered: nothing new

        await DispatchAsync(DateTime.UtcNow);
        await DispatchAsync(DateTime.UtcNow);

        var sent = Assert.Single(_fixture.Mail.SentTo(email));
        Assert.Equal("Đơn hàng 0199aa11 đã được thanh toán", sent.Subject);
        Assert.Contains("Xin chào Lan", sent.Body);
        Assert.Contains("22.462.000 VND", sent.Body);
        Assert.Contains("http://shop.test/orders/0199aa11-2233-7bbc-8ddd-eeeeffff0000", sent.Body);
        var row = await RowAsync(request.EmailId);
        Assert.Equal((OutgoingEmailStatus.Sent, 1), (row.Status, row.Attempts));
    }

    [Fact]
    public async Task It_is_written_in_the_language_asked_for_and_Vietnamese_when_there_are_no_words_for_it()
    {
        var (id, email) = await PersonAsync();

        await SendAsync(new QueueEmailCommand(Request(id, "en")));
        await SendAsync(new QueueEmailCommand(Request(id, "fr")));
        await DispatchAsync(DateTime.UtcNow);

        var subjects = _fixture.Mail.SentTo(email).Select(m => m.Subject).ToHashSet();
        Assert.Equal(new HashSet<string> { "Your order 0199aa11 is paid", "Đơn hàng 0199aa11 đã được thanh toán" }, subjects);
        Assert.Contains("22,462,000 VND", _fixture.Mail.SentTo(email).Single(m => m.Subject.StartsWith("Your")).Body);
    }

    /// <summary>The acceptance of #102: a mail server that is down delays email; it never loses it.</summary>
    [Fact]
    public async Task A_mail_server_that_is_down_delays_the_email_and_it_goes_out_when_it_is_back()
    {
        var (id, email) = await PersonAsync();
        var request = Request(id, "vi");
        await SendAsync(new QueueEmailCommand(request));
        var now = DateTime.UtcNow;

        _fixture.Mail.Down = true;
        try
        {
            await DispatchAsync(now);
        }
        finally
        {
            _fixture.Mail.Down = false;
        }

        var waiting = await RowAsync(request.EmailId);
        Assert.Equal((OutgoingEmailStatus.Pending, 1), (waiting.Status, waiting.Attempts));
        Assert.Contains("Connection refused", waiting.LastError);
        Assert.True(waiting.NextAttemptAt >= now.AddSeconds(59));

        await DispatchAsync(now);                       // not due yet: nothing is tried
        Assert.Empty(_fixture.Mail.SentTo(email));

        await DispatchAsync(now.AddMinutes(2));         // back, and due
        Assert.Single(_fixture.Mail.SentTo(email));
        Assert.Equal(OutgoingEmailStatus.Sent, (await RowAsync(request.EmailId)).Status);
    }

    [Fact]
    public async Task An_email_that_never_gets_through_gives_up_and_says_why()
    {
        var (id, email) = await PersonAsync();
        var request = Request(id, "vi");
        await SendAsync(new QueueEmailCommand(request));

        _fixture.Mail.Down = true;
        try
        {
            var at = DateTime.UtcNow;
            for (var i = 0; i < 12; i++)
            {
                await DispatchAsync(at);
                at = at.AddHours(2);   // always past the next attempt
            }
        }
        finally
        {
            _fixture.Mail.Down = false;
        }

        var row = await RowAsync(request.EmailId);
        Assert.Equal((OutgoingEmailStatus.Failed, 12), (row.Status, row.Attempts));
        Assert.Contains("Connection refused", row.LastError);
        await DispatchAsync(DateTime.UtcNow.AddDays(2));
        Assert.Empty(_fixture.Mail.SentTo(email));
    }

    [Fact]
    public async Task Nobody_to_write_to_and_no_words_to_write_are_failures_not_retries()
    {
        var nobody = Request(Guid.NewGuid(), "vi");
        var (id, email) = await PersonAsync();
        var unknown = Request(id, "vi") with { Template = "SomethingNew" };
        var incomplete = Request(id, "vi") with { Data = new() { ["orderId"] = "x" } };

        foreach (var r in new[] { nobody, unknown, incomplete })
            await SendAsync(new QueueEmailCommand(r));
        await DispatchAsync(DateTime.UtcNow);

        Assert.Equal("No such recipient.", (await RowAsync(nobody.EmailId)).LastError);
        Assert.Equal(OutgoingEmailStatus.Failed, (await RowAsync(unknown.EmailId)).Status);
        Assert.Equal(OutgoingEmailStatus.Failed, (await RowAsync(incomplete.EmailId)).Status);
        Assert.Empty(_fixture.Mail.SentTo(email));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 8)]
    [InlineData(7, 60)]    // 64 would be longer than the longest wait
    [InlineData(11, 60)]
    public void Each_failure_waits_longer_up_to_an_hour(int attempts, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), DispatchEmailsCommandHandler.Backoff(attempts));
    }

    // ------------------------------------------------------------------ helpers

    private static EmailRequested Request(Guid recipient, string language) => new(
        Guid.CreateVersion7(), recipient, EmailTemplate.OrderPaid,
        new() { ["orderId"] = "0199aa11-2233-7bbc-8ddd-eeeeffff0000", ["total"] = "22462000", ["currency"] = "VND" },
        language, DateTime.UtcNow);

    private async Task<(Guid Id, string Email)> PersonAsync()
    {
        var email = $"mail-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));
        return (registered.Id, email);
    }

    private Task DispatchAsync(DateTime now) => SendAsync(new DispatchEmailsCommand(now));

    private async Task<OutgoingEmail> RowAsync(Guid id)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutgoingEmails.AsNoTracking().SingleAsync(e => e.Id == id);
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
