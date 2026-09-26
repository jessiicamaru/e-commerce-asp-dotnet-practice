using System.Text.RegularExpressions;
using Ecommerce.Activity.Application.Common.Interfaces;
using Ecommerce.Activity.Domain.Entities;
using Ecommerce.Shared.Audit;
using Ecommerce.Shared.Authentication;
using Ecommerce.Shared.Exceptions;
using Ecommerce.Shared.Notifications;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Ecommerce.Activity.Application.Notifications;

/// <summary>
/// Which notification keys exist and what each may say (specs/078): a declared kind, or a kind with an i18next plural
/// suffix, in one of the shop's languages; its placeholders are those the kind's data can fill (notification-kinds.json).
/// </summary>
public static partial class NotificationWording
{
    public static readonly IReadOnlyList<string> Languages = ["vi", "en"];

    public const int MaxLength = 500;

    private static readonly string[] PluralSuffixes = ["zero", "one", "two", "few", "many", "other"];

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"href=""([^""]*)""")]
    private static partial Regex HrefPattern();

    /// <summary>The kind a key words - <c>NewReview_one</c> is NewReview's - or null when it names nothing declared.</summary>
    public static string? KindOf(string key)
    {
        if (NotificationContract.Kinds.ContainsKey(key))
            return key;
        var cut = key.LastIndexOf('_');
        return cut > 0 && PluralSuffixes.Contains(key[(cut + 1)..]) && NotificationContract.Kinds.ContainsKey(key[..cut])
            ? key[..cut]
            : null;
    }

    public static IReadOnlyList<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();

    /// <summary>
    /// Where a link in a notice may go: the web (http, https) or a page of the storefront (<c>/orders</c>) - never
    /// <c>//elsewhere</c>, which a browser reads as another site.
    /// </summary>
    public static IEnumerable<string> BadLinks(string html) =>
        HrefPattern().Matches(html).Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
            .Where(href => !(href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || (href.StartsWith('/') && !href.StartsWith("//"))));
}

public record NotificationWordingEntry(string Key, string Language, string? Text, bool IsDefault, int Version, DateTime UpdatedAt, Guid UpdatedBy);

public record NotificationKindWording(string Kind, IReadOnlyList<string> Placeholders);

/// <summary>What the console needs: every kind with its placeholders, and every key's current version.</summary>
public record NotificationWordingOverview(IReadOnlyList<NotificationKindWording> Kinds, IReadOnlyList<NotificationWordingEntry> Entries);

/// <summary>The storefront's view: per language, the keys an administrator has reworded and what they say now.</summary>
public record GetNotificationWordingQuery : IRequest<Dictionary<string, Dictionary<string, string>>>;

public record GetNotificationWordingOverviewQuery : IRequest<NotificationWordingOverview>;

public record GetNotificationWordingVersionsQuery(string Key, string Language) : IRequest<List<NotificationWordingEntry>>;

public record SaveNotificationWordingCommand(string Key, string Language, string Text, int ExpectedVersion) : IRequest<NotificationWordingEntry>;

public record ResetNotificationWordingCommand(string Key, string Language, int ExpectedVersion) : IRequest<NotificationWordingEntry>;

public record RestoreNotificationWordingCommand(string Key, string Language, int Version, int ExpectedVersion) : IRequest<NotificationWordingEntry>;

public class SaveNotificationWordingCommandValidator : AbstractValidator<SaveNotificationWordingCommand>
{
    public SaveNotificationWordingCommandValidator()
    {
        RuleFor(x => x.Text).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("The words are required.")
            .MaximumLength(NotificationWording.MaxLength);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}

public class NotificationWordingHandlers(
    INotificationWordingStore store,
    INoticeSanitizer sanitizer,
    ICurrentUser currentUser,
    IAuditTrail audit) :
    IRequestHandler<GetNotificationWordingQuery, Dictionary<string, Dictionary<string, string>>>,
    IRequestHandler<GetNotificationWordingOverviewQuery, NotificationWordingOverview>,
    IRequestHandler<GetNotificationWordingVersionsQuery, List<NotificationWordingEntry>>,
    IRequestHandler<SaveNotificationWordingCommand, NotificationWordingEntry>,
    IRequestHandler<ResetNotificationWordingCommand, NotificationWordingEntry>,
    IRequestHandler<RestoreNotificationWordingCommand, NotificationWordingEntry>
{
    public const string Stale = "Somebody changed these words since you opened them. Reload and make your change again.";
    public const string AlreadyDefault = "These words are already the storefront's own.";

    private readonly INotificationWordingStore _store = store;
    private readonly INoticeSanitizer _sanitizer = sanitizer;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _audit = audit;

    public async Task<Dictionary<string, Dictionary<string, string>>> Handle(GetNotificationWordingQuery request, CancellationToken cancellationToken)
    {
        var edited = (await _store.CurrentAllAsync(cancellationToken)).Where(v => !v.IsDefault && v.Text is not null).ToList();
        return NotificationWording.Languages.ToDictionary(
            lang => lang,
            lang => edited.Where(v => v.Language == lang).ToDictionary(v => v.Key, v => v.Text!));
    }

    public async Task<NotificationWordingOverview> Handle(GetNotificationWordingOverviewQuery request, CancellationToken cancellationToken)
    {
        var kinds = NotificationContract.Kinds.Keys.Order()
            .Select(kind => new NotificationKindWording(kind, NotificationContract.PlaceholdersFor(kind)))
            .ToList();
        var entries = (await _store.CurrentAllAsync(cancellationToken)).OrderBy(v => v.Key).ThenBy(v => v.Language).Select(Entry).ToList();
        return new NotificationWordingOverview(kinds, entries);
    }

    public async Task<List<NotificationWordingEntry>> Handle(GetNotificationWordingVersionsQuery request, CancellationToken cancellationToken)
    {
        Known(request.Key, request.Language);
        return (await _store.HistoryAsync(request.Key, request.Language, cancellationToken)).Select(Entry).ToList();
    }

    public async Task<NotificationWordingEntry> Handle(SaveNotificationWordingCommand request, CancellationToken cancellationToken)
    {
        var kind = Known(request.Key, request.Language);
        var text = Clean(kind, request.Text);
        return await AddVersionAsync(request.Key, request.Language, request.ExpectedVersion, text, "NotificationWordingSaved", "saved", cancellationToken);
    }

    public async Task<NotificationWordingEntry> Handle(ResetNotificationWordingCommand request, CancellationToken cancellationToken)
    {
        Known(request.Key, request.Language);
        var current = await _store.CurrentAsync(request.Key, request.Language, cancellationToken);
        if (current is null || current.IsDefault)
            throw new ConflictException(AlreadyDefault);

        return await AddVersionAsync(request.Key, request.Language, request.ExpectedVersion, null, "NotificationWordingReset",
            "reset to the storefront's words", cancellationToken);
    }

    public async Task<NotificationWordingEntry> Handle(RestoreNotificationWordingCommand request, CancellationToken cancellationToken)
    {
        var kind = Known(request.Key, request.Language);
        var earlier = await _store.GetVersionAsync(request.Key, request.Language, request.Version, cancellationToken)
            ?? throw new NotFoundException($"Version {request.Version} of these words was not found.");

        // Checked again: what was allowed then is held to today's rules.
        var text = earlier.IsDefault ? null : Clean(kind, earlier.Text!);
        return await AddVersionAsync(request.Key, request.Language, request.ExpectedVersion, text, "NotificationWordingRestored",
            $"restored to version {request.Version}", cancellationToken);
    }

    private async Task<NotificationWordingEntry> AddVersionAsync(
        string key, string language, int expected, string? text, string action, string what, CancellationToken cancellationToken)
    {
        var current = await _store.CurrentAsync(key, language, cancellationToken);
        if ((current?.Version ?? 0) != expected)
            throw new ConflictException(Stale);

        var version = new NotificationWordingVersion
        {
            Key = key,
            Language = language,
            Version = expected + 1,
            IsDefault = text is null,
            Text = text,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.Id ?? throw new UnauthorizedAccessException("The access token does not carry a valid user id."),
        };

        var before = new { Text = current is { IsDefault: false } ? current.Text : null, IsDefault = current?.IsDefault ?? true };
        var added = await _store.TryAddAsync(version, ct => _audit.RecordAsync(AuditCategory.System, action, "NotificationWording",
            $"{key}/{language}", $"The {key} notice ({language}) {what}", before, new { version.Text, version.IsDefault },
            cancellationToken: ct), cancellationToken);
        if (!added)
            throw new ConflictException(Stale);

        return Entry(version);
    }

    /// <summary>Sanitised, then checked: placeholders the kind fills, and links that stay on the web or the storefront.</summary>
    private string Clean(string kind, string text)
    {
        var clean = _sanitizer.Sanitize(text).Trim();
        var allowed = NotificationContract.PlaceholdersFor(kind);
        var failures = NotificationWording.Placeholders(clean).Where(p => !allowed.Contains(p))
            .Select(p => new ValidationFailure("Text", $"{{{{{p}}}}} is not something a {kind} notice can fill in. It can use: "
                + (allowed.Count == 0 ? "nothing" : string.Join(", ", allowed.Select(a => "{{" + a + "}}"))) + "."))
            .ToList();
        failures.AddRange(NotificationWording.BadLinks(clean)
            .Select(href => new ValidationFailure("Text", $"A link must go to a web address or a page of the shop, not \"{href}\".")));
        if (clean.Length == 0)
            failures.Add(new ValidationFailure("Text", "The words are empty."));
        if (clean.Length > NotificationWording.MaxLength)
            failures.Add(new ValidationFailure("Text", $"At most {NotificationWording.MaxLength} characters."));
        if (failures.Count > 0)
            throw new ValidationException(failures);
        return clean;
    }

    /// <summary>The key's kind - and the check that the key and language exist at all (404 otherwise).</summary>
    private static string Known(string key, string language) =>
        NotificationWording.KindOf(key) is { } kind && NotificationWording.Languages.Contains(language)
            ? kind
            : throw new NotFoundException("No such notification wording.");

    private static NotificationWordingEntry Entry(NotificationWordingVersion v) =>
        new(v.Key, v.Language, v.Text, v.IsDefault, v.Version, v.CreatedAt, v.CreatedBy);
}
