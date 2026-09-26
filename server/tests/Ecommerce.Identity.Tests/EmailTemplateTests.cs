using Ecommerce.Application.Auth.Commands.Register;
using Ecommerce.Application.Email;
using Ecommerce.Contracts.Activity;
using Ecommerce.Contracts.Identity;
using Ecommerce.Infrastructure.Persistence;
using Ecommerce.Shared.Email;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Identity.Tests;

/// <summary>
/// An administrator edits the emails (specs/077, #150): what they save is what the next email says, as HTML with a
/// plain-text alternative; a placeholder the email cannot fill is refused by name and a security email keeps its
/// link; what could run in an inbox is stripped; two saves at once make one version; and every version can be
/// undone.
/// </summary>
/// <remarks>The templates are global to the database, so every test starts and ends with none saved.</remarks>
[Collection(nameof(IdentityTestCollection))]
public class EmailTemplateTests(IdentityTestFixture fixture) : IAsyncLifetime
{
    private readonly IdentityTestFixture _fixture = fixture;
    private readonly Guid _admin = Guid.CreateVersion7();

    public Task InitializeAsync() => ForgetEditsAsync();

    public Task DisposeAsync() => ForgetEditsAsync();

    [Fact]
    public async Task An_unedited_email_says_the_built_in_words_as_html_with_a_text_alternative()
    {
        var (id, email) = await PersonAsync("Lan");

        await QueueAndDispatchAsync(id, "vi");

        var sent = Assert.Single(_fixture.Mail.SentTo(email));
        Assert.Equal("Đơn hàng 0199aa11 đã được thanh toán", sent.Subject);
        Assert.Contains("<p>Xin chào Lan,</p>", sent.Html);
        Assert.Contains("<a href=\"http://shop.test/orders/0199aa11-2233-7bbc-8ddd-eeeeffff0000\">", sent.Html);
        Assert.StartsWith("Xin chào Lan,\n\nCảm ơn bạn", sent.Body);
        Assert.Contains("Xem đơn hàng: http://shop.test/orders/0199aa11-2233-7bbc-8ddd-eeeeffff0000", sent.Body);
    }

    [Fact]
    public async Task What_an_administrator_saves_is_what_the_next_email_says_in_that_language_only()
    {
        var saved = await AsAdmin(new SaveEmailTemplateCommand(EmailTemplate.OrderPaid, "en",
            "Paid: {order}", "<h2>Thanks, {name}!</h2><p>Total <strong>{total}</strong>. <a href=\"{link}\">Track it</a></p>", 0));
        var (id, email) = await PersonAsync("Lan");

        await QueueAndDispatchAsync(id, "en");
        await QueueAndDispatchAsync(id, "vi");

        Assert.Equal((1, false), (saved.Version, saved.IsDefault));
        var english = Assert.Single(_fixture.Mail.SentTo(email), m => m.Subject == "Paid: 0199aa11");
        Assert.Contains("<h2>Thanks, Lan!</h2>", english.Html);
        Assert.Contains("Total 22,462,000 VND.", english.Body);
        Assert.Contains("Track it (http://shop.test/orders/0199aa11-2233-7bbc-8ddd-eeeeffff0000)", english.Body);
        // Vietnamese was not edited: its own built-in words, not the English edit.
        Assert.Single(_fixture.Mail.SentTo(email), m => m.Subject == "Đơn hàng 0199aa11 đã được thanh toán");
    }

    /// <summary>The issue's acceptance: refused, with a message that names the placeholder.</summary>
    [Fact]
    public async Task A_placeholder_the_email_cannot_fill_is_refused_by_name()
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() => AsAdmin(new SaveEmailTemplateCommand(
            EmailTemplate.OrderPaid, "vi", "Đơn {order} - {discount}", "<p>{name}, mã {voucher}</p>", 0)));

        Assert.Contains(refused.Errors, e => e.PropertyName == "Subject" && e.ErrorMessage.StartsWith("{discount} is not"));
        Assert.Contains(refused.Errors, e => e.PropertyName == "BodyHtml" && e.ErrorMessage.StartsWith("{voucher} is not"));
        Assert.Equal(0, (await CurrentAsync(EmailTemplate.OrderPaid, "vi")).Version);
    }

    [Theory]
    [InlineData(EmailTemplate.PasswordReset)]
    [InlineData(EmailTemplate.EmailConfirmation)]
    public async Task A_security_email_cannot_lose_its_link(string template)
    {
        var refused = await Assert.ThrowsAsync<ValidationException>(() =>
            AsAdmin(new SaveEmailTemplateCommand(template, "en", "Hello", "<p>Hi {name}, click the button.</p>", 0)));

        Assert.Contains(refused.Errors, e => e.ErrorMessage.Contains("must keep {link}"));
    }

    [Fact]
    public async Task What_could_run_in_an_inbox_is_stripped_and_a_name_is_shown_as_text()
    {
        var saved = await AsAdmin(new SaveEmailTemplateCommand(EmailTemplate.OrderPaid, "vi", "Đơn {order}",
            "<p onclick=\"steal()\" style=\"color:red\">Hi {name}<script>alert(1)</script>"
            + "<a href=\"javascript:alert(1)\">x</a><img src=\"https://evil.test/p.png\"><iframe src=\"https://evil.test\"></iframe></p>"
            + "<p><a href=\"{link}\">{link}</a></p>", 0));
        var (id, email) = await PersonAsync("<b>Mai</b>");

        await QueueAndDispatchAsync(id, "vi");

        Assert.Equal("<p>Hi {name}<a>x</a></p><p><a href=\"{link}\">{link}</a></p>", saved.BodyHtml);
        var sent = Assert.Single(_fixture.Mail.SentTo(email));
        Assert.Contains("Hi &lt;b&gt;Mai&lt;/b&gt;", sent.Html);
        Assert.DoesNotContain("<b>Mai</b>", sent.Html);
    }

    [Fact]
    public async Task A_stale_editor_is_a_409_and_two_saves_at_once_make_one_version()
    {
        await AsAdmin(new SaveEmailTemplateCommand(EmailTemplate.OrderPaid, "vi", "Một {order}", "<p>{name}</p>", 0));

        await Assert.ThrowsAsync<ConflictException>(() =>
            AsAdmin(new SaveEmailTemplateCommand(EmailTemplate.OrderPaid, "vi", "Cũ {order}", "<p>{name}</p>", 0)));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            try
            {
                await AsAdmin(new SaveEmailTemplateCommand(EmailTemplate.OrderPaid, "vi", $"Lần {i} {{order}}", "<p>{name}</p>", 1));
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }));

        Assert.Single(outcomes, won => won);
        var history = await AsAdmin(new GetEmailTemplateVersionsQuery(EmailTemplate.OrderPaid, "vi"));
        Assert.Equal([2, 1], history.Select(v => v.Version));
    }

    [Fact]
    public async Task Reset_and_restore_are_new_versions_and_each_is_audited_with_before_and_after()
    {
        await AsAdmin(new SaveEmailTemplateCommand(EmailTemplate.PasswordReset, "en", "Reset it", "<p>Go: <a href=\"{link}\">here</a></p>", 0));
        var (reset, resetAudit) = await AuditedAsync(new ResetEmailTemplateCommand(EmailTemplate.PasswordReset, "en", 1));
        await Assert.ThrowsAsync<ConflictException>(() => AsAdmin(new ResetEmailTemplateCommand(EmailTemplate.PasswordReset, "en", 2)));
        var (restored, _) = await AuditedAsync(new RestoreEmailTemplateVersionCommand(EmailTemplate.PasswordReset, "en", 1, 2));

        Assert.Equal((2, true, "Reset your password"), (reset.Version, reset.IsDefault, reset.Subject));
        Assert.Equal((3, false, "Reset it"), (restored.Version, restored.IsDefault, restored.Subject));
        var entry = Assert.Single(resetAudit, a => a.Action == "EmailTemplateReset");
        Assert.Equal("System", entry.Category);
        Assert.Equal("PasswordReset/en", entry.SubjectId);
        Assert.Contains("Reset it", entry.Before);
        Assert.Contains("Reset your password", entry.After);
        Assert.Equal([3, 2, 1], (await AsAdmin(new GetEmailTemplateVersionsQuery(EmailTemplate.PasswordReset, "en"))).Select(v => v.Version));
    }

    [Fact]
    public async Task The_list_holds_every_template_in_both_languages_with_what_each_may_use()
    {
        var list = await AsAdmin(new GetEmailTemplatesQuery());

        Assert.Equal(6, list.Count);
        var reset = list.Single(t => t.Template == EmailTemplate.PasswordReset && t.Language == "vi");
        Assert.Equal((true, 0), (reset.IsDefault, reset.Version));
        Assert.Equal(["name", "link"], reset.Placeholders);
        Assert.Equal(["link"], reset.Required);
        Assert.Contains("<a href=\"{link}\">{link}</a>", reset.BodyHtml);
    }

    [Fact]
    public async Task A_preview_fills_made_up_data_and_a_test_goes_to_the_caller_only()
    {
        var (me, myEmail) = await PersonAsync("Hoa");

        var preview = await As(me, new PreviewEmailTemplateCommand(EmailTemplate.EmailConfirmation, "en", "Confirm", "<p>Hi {name}: <a href=\"{link}\">confirm</a></p>"));
        var tested = await As(me, new SendTestEmailCommand(EmailTemplate.EmailConfirmation, "en", "Confirm", "<p>Hi {name}: <a href=\"{link}\">confirm</a></p>"));

        Assert.Contains("confirm-email?token=sample-token", preview.Html);
        Assert.Equal(preview, tested);
        var sent = Assert.Single(_fixture.Mail.SentTo(myEmail));
        Assert.Equal("[Test] Confirm", sent.Subject);
        Assert.Equal(0, (await CurrentAsync(EmailTemplate.EmailConfirmation, "en")).Version);   // nothing saved
    }

    [Fact]
    public async Task No_such_template_or_language_is_a_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => AsAdmin(new SaveEmailTemplateCommand("Newsletter", "vi", "x", "<p>x</p>", 0)));
        await Assert.ThrowsAsync<NotFoundException>(() => AsAdmin(new GetEmailTemplateVersionsQuery(EmailTemplate.OrderPaid, "fr")));
    }

    // ------------------------------------------------------------------ helpers

    private async Task QueueAndDispatchAsync(Guid recipient, string language)
    {
        await As(Guid.Empty, new QueueEmailCommand(new EmailRequested(Guid.CreateVersion7(), recipient, EmailTemplate.OrderPaid,
            new() { ["orderId"] = "0199aa11-2233-7bbc-8ddd-eeeeffff0000", ["total"] = "22462000", ["currency"] = "VND" },
            language, DateTime.UtcNow)));
        await As(Guid.Empty, new DispatchEmailsCommand(DateTime.UtcNow));
    }

    private async Task<EmailTemplateResponse> CurrentAsync(string template, string language) =>
        (await AsAdmin(new GetEmailTemplatesQuery())).Single(t => t.Template == template && t.Language == language);

    private async Task<(Guid Id, string Email)> PersonAsync(string firstName)
    {
        var email = $"tpl-{Guid.NewGuid():N}@example.test";
        var registered = await As(Guid.Empty, new RegisterCommand(email, "Passw0rd!23", firstName, "Pham"));
        await _fixture.DropConfirmationEmailAsync(registered.Id);
        return (registered.Id, email);
    }

    private async Task<(T Result, List<AuditEntryRecorded> Audit)> AuditedAsync<T>(IRequest<T> request)
    {
        await using var provider = _fixture.For(_admin, "Admin");
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        T result;
        await using (var scope = provider.CreateAsyncScope())
            result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
        return (result, harness.Published.Select<AuditEntryRecorded>().Select(x => x.Context.Message).ToList());
    }

    private Task<T> AsAdmin<T>(IRequest<T> request) => As(_admin, request, "Admin");

    private async Task<T> As<T>(Guid caller, IRequest<T> request, params string[] roles)
    {
        await using var provider = _fixture.For(caller, roles);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task ForgetEditsAsync()
    {
        await using var provider = _fixture.For(Guid.Empty);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().EmailTemplateVersions.ExecuteDeleteAsync();
    }
}
