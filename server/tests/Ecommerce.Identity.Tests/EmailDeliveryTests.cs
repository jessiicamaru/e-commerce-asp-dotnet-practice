using System.Text.Json;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Email;
using Ecommerce.Contracts.Activity;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// What became of an email (specs/087, #175): a failed one ended in the database with its reason and nobody saw it.
/// An administrator now lists them - never their data - and sends a failed one again, once.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class EmailDeliveryTests(IdentityTestFixture fixture)
{
    private static readonly Guid Admin = Guid.CreateVersion7();
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task A_failed_email_is_listed_with_why_and_who_it_was_for_and_never_its_data()
    {
        var (person, address) = await PersonAsync();
        var failed = await EmailAsync(person, EmailTemplate.OrderPaid, OutgoingEmailStatus.Failed, """{"orderId":"secret-order-7","total":"1","currency":"VND"}""");

        var page = await SendAsync(new GetOutgoingEmailsQuery("Failed", address));

        var row = Assert.Single(page.Items);
        Assert.Equal((failed, address, "OrderPaid", "Connection refused", true), (row.Id, row.RecipientEmail, row.Template, row.LastError, row.CanRetry));
        Assert.DoesNotContain("secret-order-7", JsonSerializer.Serialize(page));
    }

    [Fact]
    public async Task Each_state_is_its_own_list_and_a_search_narrows_it_to_one_person()
    {
        var (person, address) = await PersonAsync();
        var (other, _) = await PersonAsync();
        var sent = await EmailAsync(person, EmailTemplate.OrderPaid, OutgoingEmailStatus.Sent);
        await EmailAsync(person, EmailTemplate.OrderPaid, OutgoingEmailStatus.Failed);
        await EmailAsync(other, EmailTemplate.OrderPaid, OutgoingEmailStatus.Sent);

        var mine = await SendAsync(new GetOutgoingEmailsQuery("sent", address.ToUpperInvariant()));

        Assert.Equal(sent, Assert.Single(mine.Items).Id);
        Assert.False(mine.Items[0].CanRetry);   // only a failed one is sent again
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => SendAsync(new GetOutgoingEmailsQuery("Bounced")));
    }

    /// <summary>Back in the queue from its first attempt, recorded, delivered once - and a second retry is refused.</summary>
    [Fact]
    public async Task A_failed_email_sent_again_goes_out_once_and_is_on_the_record()
    {
        var (person, address) = await PersonAsync();
        var failed = await EmailAsync(person, EmailTemplate.OrderPaid, OutgoingEmailStatus.Failed);

        var (retried, audit) = await RetryWithAuditAsync(failed);
        Assert.Equal(("Pending", 0, null), (retried.Status, retried.Attempts, retried.LastError));
        var entry = Assert.Single(audit);
        Assert.Equal(("System", "EmailRetried", failed.ToString()), (entry.Category, entry.Action, entry.SubjectId));

        await SendAsync(new DispatchEmailsCommand(DateTime.UtcNow.AddSeconds(1)));
        Assert.Single(_fixture.Mail.SentTo(address));

        var again = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new RetryEmailCommand(failed)));
        Assert.Contains("only a failed one", again.Message);
    }

    /// <summary>
    /// Two administrators pressing "Send again" at the same moment (#186, specs/094): the guarded UPDATE lets exactly
    /// one through; the rest are 409 and move nothing, and the person receives the email once.
    /// </summary>
    [Fact]
    public async Task Simultaneous_retries_of_one_email_send_it_once()
    {
        var (person, address) = await PersonAsync();
        var failed = await EmailAsync(person, EmailTemplate.OrderPaid, OutgoingEmailStatus.Failed);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            try
            {
                await SendAsync(new RetryEmailCommand(failed));
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }));

        Assert.Equal(1, attempts.Count(ok => ok));
        await SendAsync(new DispatchEmailsCommand(DateTime.UtcNow.AddSeconds(1)));
        Assert.Single(_fixture.Mail.SentTo(address));
    }

    /// <summary>A reset link is dead after 30 minutes: sending it later helps nobody. The person asks for a new one.</summary>
    [Fact]
    public async Task A_reset_or_confirmation_link_is_never_sent_again()
    {
        var (person, _) = await PersonAsync();
        var reset = await EmailAsync(person, EmailTemplate.PasswordReset, OutgoingEmailStatus.Failed, """{"token":"t"}""");

        Assert.False(Assert.Single((await SendAsync(new GetOutgoingEmailsQuery("Failed", null, 1, 50))).Items, e => e.Id == reset).CanRetry);
        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(new RetryEmailCommand(reset)));
        Assert.Equal(OutgoingEmailStatus.Failed, await StatusAsync(reset));
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(new RetryEmailCommand(Guid.CreateVersion7())));
    }

    // ------------------------------------------------------------------ helpers

    private async Task<(Guid Id, string Email)> PersonAsync()
    {
        var email = $"delivery-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(new RegisterCommand(email, "Passw0rd!23", "Lan", "Pham"));
        await _fixture.DropConfirmationEmailAsync(registered.Id);
        return (registered.Id, email);
    }

    /// <summary>An email as the dispatcher would have left it.</summary>
    private async Task<Guid> EmailAsync(Guid recipient, string template, OutgoingEmailStatus status, string data =
        """{"orderId":"0199aa11-2233-7bbc-8ddd-eeeeffff0000","total":"22462000","currency":"VND"}""")
    {
        var id = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OutgoingEmails.Add(new OutgoingEmail
        {
            Id = id, RecipientId = recipient, Template = template, DataJson = data, Language = "en", Status = status,
            Attempts = status == OutgoingEmailStatus.Failed ? 12 : 1,
            LastError = status == OutgoingEmailStatus.Failed ? "Connection refused" : null,
            NextAttemptAt = now.AddYears(1), SentAt = status == OutgoingEmailStatus.Sent ? now : null, CreatedAt = now,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<OutgoingEmailStatus> StatusAsync(Guid id)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutgoingEmails
            .Where(e => e.Id == id).Select(e => e.Status).SingleAsync();
    }

    private async Task<(OutgoingEmailResponse, List<AuditEntryRecorded>)> RetryWithAuditAsync(Guid id)
    {
        await using var provider = _fixture.For(Admin, ["Admin"]);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        OutgoingEmailResponse retried;
        await using (var scope = provider.CreateAsyncScope())
        {
            retried = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RetryEmailCommand(id));
        }

        return (retried, harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).ToList());
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(Admin, ["Admin"]);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
