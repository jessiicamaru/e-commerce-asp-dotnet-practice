using Ecommerce.Application.Common.Interfaces;
using Ecommerce.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace Ecommerce.Application.Email;

/// <summary>The saved versions of the emails' words (specs/077).</summary>
public interface IEmailTemplateStore
{
    /// <summary>The newest version of one template in one language - null when nobody ever edited it.</summary>
    Task<EmailTemplateVersion?> CurrentAsync(string template, string language, CancellationToken cancellationToken = default);

    /// <summary>The newest version of every template and language that has one.</summary>
    Task<List<EmailTemplateVersion>> CurrentAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Every version of one template in one language, newest first.</summary>
    Task<List<EmailTemplateVersion>> HistoryAsync(string template, string language, CancellationToken cancellationToken = default);

    Task<EmailTemplateVersion?> GetVersionAsync(string template, string language, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a version unless its number is taken: <c>INSERT ... ON CONFLICT DO NOTHING</c> on (template, language,
    /// version), and only if it inserted, <paramref name="stage"/> (the audit entry) and a save - one transaction.
    /// False means somebody saved that number first.
    /// </summary>
    Task<bool> TryAddAsync(EmailTemplateVersion version, Func<CancellationToken, Task> stage, CancellationToken cancellationToken = default);
}

/// <summary>An allow-list HTML sanitiser (specs/077): what an email's body may contain, and nothing else.</summary>
public interface IHtmlSanitizer
{
    string Sanitize(string html);
}

/// <summary>
/// Puts an email together for sending (specs/077): the administrator's current words for its template and language -
/// sanitised again, whatever was checked on save - or the built-in ones, filled in for one recipient.
/// </summary>
public class EmailComposer(IEmailTemplateStore store, IHtmlSanitizer sanitizer)
{
    private readonly IEmailTemplateStore _store = store;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;

    public async Task<RenderedEmail?> ComposeAsync(
        string template, string language, IReadOnlyDictionary<string, string> data, string recipientName, string storefrontUrl,
        CancellationToken cancellationToken = default)
    {
        // A language nobody writes in reads the default language - its edit, not the built-in words.
        var lang = EmailTemplates.Languages.Contains(language) ? language : EmailTemplates.DefaultLanguage;
        var current = await _store.CurrentAsync(template, lang, cancellationToken);
        var edited = current is { IsDefault: false, Subject: { } subject, BodyHtml: { } body }
            ? new EmailWords(subject, _sanitizer.Sanitize(body))
            : null;
        return EmailTemplates.Render(template, lang, data, recipientName, storefrontUrl, edited);
    }
}

public record EmailTemplateResponse(
    string Template,
    string Language,
    string Subject,
    string BodyHtml,
    bool IsDefault,
    int Version,
    DateTime? UpdatedAt,
    Guid? UpdatedBy,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<string> Required);

public record EmailTemplateVersionResponse(int Version, bool IsDefault, string Subject, string BodyHtml, DateTime CreatedAt, Guid CreatedBy);

public record EmailPreviewResponse(string Subject, string Html, string Text);

public record GetEmailTemplatesQuery : IRequest<List<EmailTemplateResponse>>;

public record GetEmailTemplateVersionsQuery(string Template, string Language) : IRequest<List<EmailTemplateVersionResponse>>;

/// <summary>Saves new words. <paramref name="ExpectedVersion"/> is the version the editor opened: a stale one is a 409.</summary>
public record SaveEmailTemplateCommand(string Template, string Language, string Subject, string BodyHtml, int ExpectedVersion)
    : IRequest<EmailTemplateResponse>;

public record ResetEmailTemplateCommand(string Template, string Language, int ExpectedVersion) : IRequest<EmailTemplateResponse>;

public record RestoreEmailTemplateVersionCommand(string Template, string Language, int Version, int ExpectedVersion)
    : IRequest<EmailTemplateResponse>;

/// <summary>A draft rendered with made-up data - nothing saved, nothing sent.</summary>
public record PreviewEmailTemplateCommand(string Template, string Language, string Subject, string BodyHtml) : IRequest<EmailPreviewResponse>;

/// <summary>A draft rendered with made-up data and sent to the caller's own address - nothing saved.</summary>
public record SendTestEmailCommand(string Template, string Language, string Subject, string BodyHtml) : IRequest<EmailPreviewResponse>;

public class SaveEmailTemplateCommandValidator : AbstractValidator<SaveEmailTemplateCommand>
{
    public SaveEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Subject).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("The subject is required.").MaximumLength(200);
        RuleFor(x => x.BodyHtml).Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("The body is required.").MaximumLength(20000);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public class PreviewEmailTemplateCommandValidator : AbstractValidator<PreviewEmailTemplateCommand>
{
    public PreviewEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Subject).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("The subject is required.").MaximumLength(200);
        RuleFor(x => x.BodyHtml).Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("The body is required.").MaximumLength(20000);
    }
}

public class SendTestEmailCommandValidator : AbstractValidator<SendTestEmailCommand>
{
    public SendTestEmailCommandValidator()
    {
        RuleFor(x => x.Subject).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("The subject is required.").MaximumLength(200);
        RuleFor(x => x.BodyHtml).Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("The body is required.").MaximumLength(20000);
    }
}

public class EmailTemplateHandlers(
    IEmailTemplateStore store,
    IHtmlSanitizer sanitizer,
    IEmailTransport transport,
    IUserRepository users,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IOptions<EmailOptions> options) :
    IRequestHandler<GetEmailTemplatesQuery, List<EmailTemplateResponse>>,
    IRequestHandler<GetEmailTemplateVersionsQuery, List<EmailTemplateVersionResponse>>,
    IRequestHandler<SaveEmailTemplateCommand, EmailTemplateResponse>,
    IRequestHandler<ResetEmailTemplateCommand, EmailTemplateResponse>,
    IRequestHandler<RestoreEmailTemplateVersionCommand, EmailTemplateResponse>,
    IRequestHandler<PreviewEmailTemplateCommand, EmailPreviewResponse>,
    IRequestHandler<SendTestEmailCommand, EmailPreviewResponse>
{
    public const string Stale = "Somebody changed this email since you opened it. Reload it and make your change again.";
    public const string AlreadyDefault = "This email already uses the built-in words.";

    private readonly IEmailTemplateStore _store = store;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;
    private readonly IEmailTransport _transport = transport;
    private readonly IUserRepository _users = users;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;
    private readonly EmailOptions _options = options.Value;

    public async Task<List<EmailTemplateResponse>> Handle(GetEmailTemplatesQuery request, CancellationToken cancellationToken)
    {
        var current = (await _store.CurrentAllAsync(cancellationToken)).ToDictionary(v => (v.Template, v.Language));
        return EmailTemplates.Templates
            .SelectMany(t => EmailTemplates.Languages.Select(l => Response(t, l, current.GetValueOrDefault((t, l)))))
            .ToList();
    }

    public async Task<List<EmailTemplateVersionResponse>> Handle(GetEmailTemplateVersionsQuery request, CancellationToken cancellationToken)
    {
        var builtIn = BuiltIn(request.Template, request.Language);
        return (await _store.HistoryAsync(request.Template, request.Language, cancellationToken))
            .Select(v => new EmailTemplateVersionResponse(v.Version, v.IsDefault, v.Subject ?? builtIn.Subject, v.BodyHtml ?? builtIn.BodyHtml, v.CreatedAt, v.CreatedBy))
            .ToList();
    }

    public async Task<EmailTemplateResponse> Handle(SaveEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        BuiltIn(request.Template, request.Language);
        var words = Clean(request.Template, request.Subject, request.BodyHtml);
        return await AddVersionAsync(request.Template, request.Language, request.ExpectedVersion, words, "EmailTemplateSaved",
            "saved", cancellationToken);
    }

    public async Task<EmailTemplateResponse> Handle(ResetEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        BuiltIn(request.Template, request.Language);
        var current = await _store.CurrentAsync(request.Template, request.Language, cancellationToken);
        if (current is null || current.IsDefault)
            throw new ConflictException(AlreadyDefault);

        return await AddVersionAsync(request.Template, request.Language, request.ExpectedVersion, null, "EmailTemplateReset",
            "reset to the built-in words", cancellationToken);
    }

    public async Task<EmailTemplateResponse> Handle(RestoreEmailTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        BuiltIn(request.Template, request.Language);
        var earlier = await _store.GetVersionAsync(request.Template, request.Language, request.Version, cancellationToken)
            ?? throw new NotFoundException($"Version {request.Version} of this email was not found.");

        // Restored words are checked again: what was allowed then is sanitised by today's rules.
        var words = earlier.IsDefault ? null : Clean(request.Template, earlier.Subject!, earlier.BodyHtml!);
        return await AddVersionAsync(request.Template, request.Language, request.ExpectedVersion, words, "EmailTemplateRestored",
            $"restored to version {request.Version}", cancellationToken);
    }

    public Task<EmailPreviewResponse> Handle(PreviewEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        BuiltIn(request.Template, request.Language);
        var rendered = Sample(request.Template, request.Language, Clean(request.Template, request.Subject, request.BodyHtml));
        return Task.FromResult(new EmailPreviewResponse(rendered.Subject, rendered.Html, rendered.Text));
    }

    public async Task<EmailPreviewResponse> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        BuiltIn(request.Template, request.Language);
        var rendered = Sample(request.Template, request.Language, Clean(request.Template, request.Subject, request.BodyHtml));
        var me = await _users.GetByIdAsync(Caller(), cancellationToken)
            ?? throw new UnauthorizedAccessException("The access token does not name an account.");

        try
        {
            await _transport.SendAsync(me.Email, $"[Test] {rendered.Subject}", rendered.Text, rendered.Html, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DependencyUnavailableException($"The mail server did not take the test email: {exception.Message}");
        }

        return new EmailPreviewResponse(rendered.Subject, rendered.Html, rendered.Text);
    }

    /// <summary>
    /// One new version on top of <paramref name="expected"/>: a stale editor, or somebody saving the same number
    /// first, is the one 409. <paramref name="words"/> null means the built-in words.
    /// </summary>
    private async Task<EmailTemplateResponse> AddVersionAsync(
        string template, string language, int expected, EmailWords? words, string action, string what, CancellationToken cancellationToken)
    {
        var current = await _store.CurrentAsync(template, language, cancellationToken);
        if ((current?.Version ?? 0) != expected)
            throw new ConflictException(Stale);

        var before = Effective(template, language, current);
        var version = new EmailTemplateVersion
        {
            Template = template,
            Language = language,
            Version = expected + 1,
            IsDefault = words is null,
            Subject = words?.Subject,
            BodyHtml = words?.BodyHtml,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Caller(),
        };
        var after = Effective(template, language, version);

        var added = await _store.TryAddAsync(version, ct => _audit.RecordAsync(AuditCategory.System, action, "EmailTemplate",
            $"{template}/{language}", $"The {template} email ({language}) {what}",
            new { before.Subject, before.BodyHtml, before.IsDefault }, new { after.Subject, after.BodyHtml, after.IsDefault },
            cancellationToken: ct), cancellationToken);
        if (!added)
            throw new ConflictException(Stale);

        return Response(template, language, version);
    }

    /// <summary>
    /// Sanitised, then checked: every placeholder is one this template fills, and none it needs is gone - a refusal
    /// that names the placeholder, as a 400 on the field it is in.
    /// </summary>
    private EmailWords Clean(string template, string subject, string bodyHtml)
    {
        var cleanSubject = subject.Replace("\r", " ").Replace("\n", " ").Trim();
        var cleanBody = _sanitizer.Sanitize(bodyHtml).Trim();
        var allowed = EmailTemplates.PlaceholdersOf[template];
        var failures = new List<ValidationFailure>();

        foreach (var (field, text) in new[] { ("Subject", cleanSubject), ("BodyHtml", cleanBody) })
        {
            foreach (var unknown in EmailHtml.Placeholders(text).Where(p => !allowed.Contains(p)))
                failures.Add(new ValidationFailure(field, $"{{{unknown}}} is not something this email can fill in. It can use: {string.Join(", ", allowed.Select(a => "{" + a + "}"))}."));
        }

        var used = EmailHtml.Placeholders(cleanBody);
        foreach (var required in EmailTemplates.RequiredOf[template].Where(r => !used.Contains(r)))
            failures.Add(new ValidationFailure("BodyHtml", $"This email must keep {{{required}}} - without it, it cannot do what it is for."));

        if (EmailHtml.ToText(cleanBody).Length == 0)
            failures.Add(new ValidationFailure("BodyHtml", "The body is empty."));

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return new EmailWords(cleanSubject, cleanBody);
    }

    private RenderedEmail Sample(string template, string language, EmailWords words)
    {
        var name = string.IsNullOrWhiteSpace(_currentUser.GivenName) ? "Mai" : _currentUser.GivenName!;
        return EmailTemplates.Render(template, language, EmailTemplates.SampleData(template), name, _options.StorefrontUrl, words)
            ?? throw new InvalidOperationException("The sample data must fill every template.");
    }

    private static EmailTemplateResponse Response(string template, string language, EmailTemplateVersion? current)
    {
        var words = Effective(template, language, current);
        return new EmailTemplateResponse(template, language, words.Subject, words.BodyHtml, words.IsDefault, current?.Version ?? 0,
            current?.CreatedAt, current?.CreatedBy, EmailTemplates.PlaceholdersOf[template], EmailTemplates.RequiredOf[template]);
    }

    private static (string Subject, string BodyHtml, bool IsDefault) Effective(string template, string language, EmailTemplateVersion? version)
    {
        if (version is { IsDefault: false, Subject: { } subject, BodyHtml: { } body })
            return (subject, body, false);
        var builtIn = BuiltIn(template, language);
        return (builtIn.Subject, builtIn.BodyHtml, true);
    }

    /// <summary>The built-in words - and the check that this template and language exist at all (404 otherwise).</summary>
    private static EmailWords BuiltIn(string template, string language) =>
        EmailTemplates.Templates.Contains(template) && EmailTemplates.Languages.Contains(language)
            ? EmailTemplates.Default(template, language)!
            : throw new NotFoundException("No such email template.");

    private Guid Caller() =>
        _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id.");
}
