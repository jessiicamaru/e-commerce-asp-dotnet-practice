using System.Reflection;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Refresh;
using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Email;
using Ecommerce.Application.Users;
using Ecommerce.Contracts.Identity;
using Ecommerce.Domain.Constants;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Email;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// The emails of specs/083 (#167): what reached a person only as a notice in the bell - a parcel shipped, an order
/// cancelled, a return decided, a saved product back, an account stopped - and the language each is written in.
/// </summary>
[Collection(nameof(IdentityTestCollection))]
public class AccountEmailTests(IdentityTestFixture fixture)
{
    private const string Password = "Passw0rd!23";
    private readonly IdentityTestFixture _fixture = fixture;
    private static readonly Guid Admin = Guid.CreateVersion7();

    /// <summary>
    /// Every template the code can ask for has words in every language, is listed for the console, and renders its
    /// own sample with nothing left unfilled - a new template missing any of it is a hole nobody sees until it is sent.
    /// </summary>
    [Fact]
    public void Every_template_has_words_in_every_language_and_renders_its_sample_whole()
    {
        var inCode = typeof(EmailTemplate).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name != nameof(EmailTemplate.ReadersLanguage))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(inCode.Order(), EmailTemplates.Templates.Order());
        foreach (var template in inCode)
        {
            foreach (var language in EmailTemplates.Languages)
            {
                var words = EmailTemplates.Default(template, language);
                Assert.NotNull(words);
                Assert.Empty(EmailHtml.Placeholders(words.Subject + words.BodyHtml).Except(EmailTemplates.PlaceholdersOf[template]));

                var rendered = EmailTemplates.Render(template, language, EmailTemplates.SampleData(template), "Lan", "http://shop.test");
                Assert.NotNull(rendered);
                Assert.DoesNotContain("{", rendered.Subject + rendered.Text);
            }
        }
    }

    [Fact]
    public void A_parcel_shipped_email_names_the_shop_the_tracking_and_the_order()
    {
        var english = EmailTemplates.Render(EmailTemplate.ParcelShipped, "en",
            new Dictionary<string, string> { ["orderId"] = "0199aa11-2233-7bbc", ["tracking"] = "VNPOST-9", ["shop"] = "Leica Corner" },
            "Lan", "http://shop.test")!;
        Assert.Equal("Your order 0199aa11 is on its way", english.Subject);
        Assert.Contains("from Leica Corner", english.Text);
        Assert.Contains("VNPOST-9", english.Text);
        Assert.Contains("http://shop.test/orders/0199aa11-2233-7bbc", english.Text);

        // The shop's own parcel names no seller.
        var ours = EmailTemplates.Render(EmailTemplate.ParcelShipped, "vi",
            new Dictionary<string, string> { ["orderId"] = "0199aa11-2233-7bbc", ["tracking"] = "VNPOST-9" }, "Lan", "http://shop.test")!;
        Assert.Contains("từ e-commerce", ours.Text);
    }

    [Fact]
    public void A_lock_says_until_when_in_utc_in_the_readers_words()
    {
        var data = new Dictionary<string, string> { ["until"] = "2031-01-15T03:30:00.0000000Z", ["reason"] = "Spam" };

        Assert.Contains("until Jan 15, 2031 03:30 UTC", EmailTemplates.Render(EmailTemplate.AccountLocked, "en", data, "Lan", "http://shop.test")!.Text);
        Assert.Contains("đến 03:30 15/01/2031 UTC", EmailTemplates.Render(EmailTemplate.AccountLocked, "vi", data, "Lan", "http://shop.test")!.Text);
        Assert.Null(EmailTemplates.Render(EmailTemplate.AccountLocked, "en", new Dictionary<string, string> { ["reason"] = "Spam" }, "Lan", "http://shop.test"));
    }

    /// <summary>Signed out by a lock or a ban, the bell is the one place they cannot look: each is emailed, once.</summary>
    [Fact]
    public async Task A_lock_and_a_ban_each_ask_for_one_email_in_the_readers_language()
    {
        var (id, _) = await NewUserAsync("en");

        var locked = Assert.Single(await EmailsAsync(new LockUserCommand(id, 2, "Cooling off")));
        Assert.Equal((id, EmailTemplate.AccountLocked, EmailTemplate.ReadersLanguage), (locked.RecipientId, locked.Template, locked.Language));
        Assert.Equal("Cooling off", locked.Data["reason"]);

        var banned = Assert.Single(await EmailsAsync(new BanUserCommand(id, "Fraud")));
        Assert.Equal((EmailTemplate.AccountBanned, "Fraud"), (banned.Template, banned.Data["reason"]));

        // Queued, it is written in the language the person last used the shop in.
        await SendAsync(Guid.Empty, new QueueEmailCommand(banned));
        Assert.Equal("en", await QueuedLanguageAsync(banned.EmailId));
    }

    [Fact]
    public async Task A_refused_lock_asks_for_no_email()
    {
        var (id, _) = await NewUserAsync("en");

        // A moderator may lock for at most 30 days (specs/043).
        Assert.Empty(await EmailsAsync(new LockUserCommand(id, 90, "Too long"), RoleNames.Moderator, expectRefusal: true));
    }

    /// <summary>
    /// The account has no language setting; the shop learns it from how the person uses it - at sign-up, at sign-in
    /// and at every session renewal, so a switch in the storefront reaches their emails within minutes.
    /// </summary>
    [Fact]
    public async Task The_language_is_learnt_at_sign_up_sign_in_and_renewal_and_nonsense_changes_nothing()
    {
        var (id, email) = await NewUserAsync("en-US");
        Assert.Equal("en", await LanguageAsync(id));

        var session = await SendAsync(Guid.Empty, new LoginCommand(email, Password, "vi"));
        Assert.Equal("vi", await LanguageAsync(id));

        session = await SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken, "en"));
        Assert.Equal("en", await LanguageAsync(id));

        await SendAsync(Guid.Empty, new RefreshTokenCommand(session.RefreshToken, "tlh"));
        await SendAsync(Guid.Empty, new LoginCommand(email, Password, ""));
        Assert.Equal("en", await LanguageAsync(id));
    }

    [Fact]
    public async Task An_email_asked_for_in_the_readers_language_is_written_in_theirs_or_the_default()
    {
        var (english, _) = await NewUserAsync("en");
        var (silent, _) = await NewUserAsync("");
        Assert.Null(await LanguageAsync(silent));

        var toEnglish = Request(english, EmailTemplate.ReadersLanguage);
        var toSilent = Request(silent, EmailTemplate.ReadersLanguage);
        var askedInVietnamese = Request(english, "vi");   // a sender who knows - an order's own language - wins
        foreach (var request in new[] { toEnglish, toSilent, askedInVietnamese })
        {
            await SendAsync(Guid.Empty, new QueueEmailCommand(request));
        }

        Assert.Equal("en", await QueuedLanguageAsync(toEnglish.EmailId));
        Assert.Equal(EmailTemplates.DefaultLanguage, await QueuedLanguageAsync(toSilent.EmailId));
        Assert.Equal("vi", await QueuedLanguageAsync(askedInVietnamese.EmailId));
    }

    // ------------------------------------------------------------------ helpers

    private static EmailRequested Request(Guid recipient, string language) => new(
        Guid.CreateVersion7(), recipient, EmailTemplate.SavedBackInStock,
        new() { ["productId"] = Guid.CreateVersion7().ToString(), ["product"] = "Fujifilm X-T5" }, language, DateTime.UtcNow);

    private async Task<(Guid Id, string Email)> NewUserAsync(string language)
    {
        var email = $"acct-{Guid.NewGuid():N}@example.test";
        var registered = await SendAsync(Guid.Empty, new RegisterCommand(email, Password, "Test", "Person", language));
        await _fixture.DropConfirmationEmailAsync(registered.Id);
        return (registered.Id, email);
    }

    /// <summary>The emails a staff request asked for - none, when it was refused.</summary>
    private async Task<List<EmailRequested>> EmailsAsync<T>(IRequest<T> request, string role = RoleNames.Admin, bool expectRefusal = false)
    {
        await using var provider = _fixture.For(Admin, [role]);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await using (var scope = provider.CreateAsyncScope())
        {
            var send = scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
            if (expectRefusal)
                Assert.NotNull(await Record.ExceptionAsync(() => send));
            else
                await send;
        }

        return harness.Published.Select<EmailRequested>().Select(x => x.Context.Message).ToList();
    }

    private async Task<string?> LanguageAsync(Guid id)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(u => u.Id == id).Select(u => u.Language).SingleAsync();
    }

    private async Task<string> QueuedLanguageAsync(Guid emailId)
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutgoingEmails
            .Where(e => e.Id == emailId).Select(e => e.Language).SingleAsync();
    }

    private async Task<T> SendAsync<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }
}
