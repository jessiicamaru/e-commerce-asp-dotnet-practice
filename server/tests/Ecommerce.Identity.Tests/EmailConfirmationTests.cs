using System.Text.Json;
using Ecommerce.Application.Auth.Commands.EmailConfirmation;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Auth.Commands.RegisterSeller;
using Ecommerce.Application.Email;
using Ecommerce.Application.ShopApplications;
using Ecommerce.Contracts.Activity;
using Ecommerce.Domain.Constants;
using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Exceptions;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// An address is confirmed by the link sent to it (specs/063, #106): once, within 24 hours. Until then the
/// account browses and buys, but does not open a shop.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class EmailConfirmationTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;

    [Fact]
    public async Task Registering_sends_one_link_in_the_language_asked_for_and_the_account_is_unconfirmed()
    {
        var email = AnEmail();
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Lan", "Pham", "en"));

        Assert.False(registered.EmailConfirmed);
        var mail = Assert.Single(await ConfirmationEmailsAsync(registered.Id));
        Assert.Equal("en", mail.Language);
        Assert.Null(await ConfirmedAtAsync(registered.Id));
    }

    [Fact]
    public async Task Registering_to_sell_sends_a_link_too()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "Mai", "Tran", "Mai Lens"));

        Assert.False(registered.EmailConfirmed);
        Assert.Single(await ConfirmationEmailsAsync(registered.Id));
    }

    [Fact]
    public async Task The_link_confirms_the_address_and_sign_in_and_refresh_say_so()
    {
        var email = AnEmail();
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Lan", "Pham"));
        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password));
        Assert.False(session.EmailConfirmed);

        await SendAsync(Guid.Empty, new ConfirmEmailCommand(await TokenAsync(registered.Id)));

        Assert.NotNull(await ConfirmedAtAsync(registered.Id));
        Assert.True((await SendAsync(Guid.Empty, new LoginCommand(email, Password))).EmailConfirmed);
        Assert.True((await SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken))).EmailConfirmed);
    }

    [Fact]
    public async Task A_link_works_once_and_not_after_it_expires_and_a_made_up_one_never_does()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Lan", "Pham"));
        var used = await TokenAsync(registered.Id);
        await SendAsync(Guid.Empty, new ConfirmEmailCommand(used));

        await RefusedAsync(used);
        await RefusedAsync("made-up-token");

        var other = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Minh", "Tran"));
        var expired = await TokenAsync(other.Id);
        await WithDbAsync(db => db.EmailConfirmationTokens.Where(t => t.UserId == other.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        await RefusedAsync(expired);
        Assert.Null(await ConfirmedAtAsync(other.Id));
    }

    /// <summary>One link, submitted at once from two tabs: one confirms, the other is refused like a used link.</summary>
    [Fact]
    public async Task Two_submissions_of_one_link_at_once_confirm_once()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Lan", "Pham"));
        var token = await TokenAsync(registered.Id);
        await using var provider = _fixture.For(Guid.Empty);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var scope = provider.CreateAsyncScope();
            try { await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ConfirmEmailCommand(token)); return true; }
            catch (FluentValidation.ValidationException) { return false; }
        }));

        Assert.Equal(1, outcomes.Count(ok => ok));
        Assert.Single(harness.Published.Select<AuditEntryRecorded>(),
            e => e.Context.Message.Action == "EmailConfirmed" && e.Context.Message.SubjectId == registered.Id.ToString());
    }

    // ------------------------------------------------------------------ send it again

    [Fact]
    public async Task Sending_it_again_replaces_the_link()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Lan", "Pham"));
        var first = await TokenAsync(registered.Id);
        await SentLongAgoAsync(registered.Id);

        await SendAsync(registered.Id, new ResendConfirmationCommand("vi"));

        var second = (await ConfirmationEmailsAsync(registered.Id)).Select(TokenOf).Single(t => t != first);
        await RefusedAsync(first);
        await SendAsync(Guid.Empty, new ConfirmEmailCommand(second));
        Assert.NotNull(await ConfirmedAtAsync(registered.Id));
    }

    [Fact]
    public async Task Sending_it_again_within_a_minute_sends_nothing_even_many_times_at_once()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Lan", "Pham"));

        await SendAsync(registered.Id, new ResendConfirmationCommand());
        Assert.Single(await ConfirmationEmailsAsync(registered.Id));

        await SentLongAgoAsync(registered.Id);
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(registered.Id, new ResendConfirmationCommand())));
        Assert.Equal(2, (await ConfirmationEmailsAsync(registered.Id)).Count);
    }

    [Fact]
    public async Task A_confirmed_address_needs_no_new_link()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Lan", "Pham"));
        await SendAsync(Guid.Empty, new ConfirmEmailCommand(await TokenAsync(registered.Id)));

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(registered.Id, new ResendConfirmationCommand()));
    }

    // ------------------------------------------------------------------ what waits for it

    [Fact]
    public async Task An_unconfirmed_customer_cannot_apply_to_sell()
    {
        var customer = await SendAsync(Guid.Empty, new RegisterCommand(AnEmail(), Password, "Lan", "Pham"));

        var refused = await Assert.ThrowsAsync<ForbiddenException>(() =>
            SendAsync(customer.Id, new ApplyForShopCommand("Lan Film", null, null)));
        Assert.Equal("EmailNotConfirmed", refused.Facts["code"]);

        await SendAsync(Guid.Empty, new ConfirmEmailCommand(await TokenAsync(customer.Id)));
        Assert.Equal("Pending", (await SendAsync(customer.Id, new ApplyForShopCommand("Lan Film", null, null))).Status);
    }

    /// <summary>Registering to sell creates the application; the shop waits for the address, and staff can see why.</summary>
    [Fact]
    public async Task An_application_is_approved_only_once_the_address_is_confirmed()
    {
        var registered = await SendAsync(Guid.Empty, new RegisterSellerCommand(AnEmail(), Password, "Mai", "Tran", "Mai Lens"));
        var id = (await SendAsync(registered.Id, new GetMyShopApplicationsQuery())).Single().Id;
        var moderator = Guid.CreateVersion7();

        // The pending queue is oldest first and other tests leave applications in it: look for this one.
        ShopApplicationResponse? listed = null;
        for (var page = 1; listed is null; page++)
        {
            var queue = await SendAsync(moderator, new GetShopApplicationsQuery("Pending", page, 50), RoleNames.Moderator);
            Assert.NotEmpty(queue.Items);
            listed = queue.Items.SingleOrDefault(a => a.Id == id);
        }

        Assert.False(listed.ApplicantEmailConfirmed);
        await Assert.ThrowsAsync<ConflictException>(() =>
            SendAsync(moderator, new ApproveShopApplicationCommand(id), RoleNames.Moderator));

        await SendAsync(Guid.Empty, new ConfirmEmailCommand(await TokenAsync(registered.Id)));

        Assert.Equal("Approved", (await SendAsync(moderator, new ApproveShopApplicationCommand(id), RoleNames.Moderator)).Status);
    }

    // ------------------------------------------------------------------ nothing secret kept

    [Fact]
    public async Task The_link_is_in_the_email_and_nowhere_else_once_it_is_sent()
    {
        var email = AnEmail();
        await using var provider = _fixture.For(Guid.Empty);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        Guid id;
        await using (var scope = provider.CreateAsyncScope())
            id = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RegisterCommand(email, Password, "Lan", "Pham"))).Id;
        var token = await TokenAsync(id);

        var stored = await WithDbAsync(db => db.EmailConfirmationTokens.AsNoTracking().SingleAsync(t => t.UserId == id));
        Assert.NotEqual(token, stored.TokenHash);
        Assert.DoesNotContain(harness.Published.Select<AuditEntryRecorded>(),
            e => (e.Context.Message.Summary + e.Context.Message.After).Contains(token));

        await SendAsync(Guid.Empty, new DispatchEmailsCommand(DateTime.UtcNow));
        var sent = Assert.Single(_fixture.Mail.SentTo(email), m => m.Subject == "Xác nhận địa chỉ email của bạn");
        Assert.Contains($"http://shop.test/confirm-email?token={Uri.EscapeDataString(token)}", sent.Body);
        var row = await WithDbAsync(db => db.OutgoingEmails.AsNoTracking().SingleAsync(e => e.RecipientId == id && e.Template == "EmailConfirmation"));
        Assert.Equal("{}", row.DataJson);
    }

    // ------------------------------------------------------------------ helpers

    private static string AnEmail() => $"confirm-{Guid.NewGuid():N}@example.test";

    private async Task RefusedAsync(string token)
    {
        var refused = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            SendAsync(Guid.Empty, new ConfirmEmailCommand(token)));
        Assert.Contains(ConfirmationTokens.Invalid, refused.Message);
    }

    private Task<DateTime?> ConfirmedAtAsync(Guid id) =>
        WithDbAsync(db => db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.EmailConfirmedAt).SingleAsync());

    private Task<List<OutgoingEmail>> ConfirmationEmailsAsync(Guid id) =>
        WithDbAsync(db => db.OutgoingEmails.AsNoTracking()
            .Where(e => e.RecipientId == id && e.Template == "EmailConfirmation").OrderBy(e => e.CreatedAt).ToListAsync());

    /// <summary>The token as the newest email carries it - what a person would click.</summary>
    private async Task<string> TokenAsync(Guid id) => TokenOf((await ConfirmationEmailsAsync(id)).Last());

    private static string TokenOf(OutgoingEmail email) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(email.DataJson)!["token"];

    /// <summary>Moves this person's links two minutes back, past the one-email-a-minute interval.</summary>
    private Task SentLongAgoAsync(Guid id) =>
        WithDbAsync(db => db.EmailConfirmationTokens.Where(t => t.UserId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, DateTime.UtcNow.AddMinutes(-2))));

    private async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> work)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private async Task SendAsync(Guid caller, IRequest request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
