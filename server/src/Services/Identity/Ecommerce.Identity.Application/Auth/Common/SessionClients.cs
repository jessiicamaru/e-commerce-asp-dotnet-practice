using Ecommerce.Domain.Entities;

namespace Ecommerce.Application.Auth.Common;

/// <summary>
/// The rule that turns a request's <c>Origin</c> into an application (#278, specs/138 research D1). A browser writes
/// <c>Origin</c> itself on every POST and no script can change it, so a page on the storefront cannot ask for a
/// back-office session. Compared by scheme, host and port, case-insensitively, without a trailing slash.
/// </summary>
public static class SessionClients
{
    public const string SectionName = "BackOffice:Origins";

    public static SessionClient FromOrigin(string? origin, IEnumerable<string> backOfficeOrigins)
    {
        var asked = Normalise(origin);
        return asked is not null && backOfficeOrigins.Select(Normalise).Any(known => known == asked)
            ? SessionClient.BackOffice
            : SessionClient.Storefront;
    }

    /// <summary>The configured origins: one comma-separated setting, so the binder has no array to append to.</summary>
    public static IReadOnlyList<string> Parse(string? setting) =>
        (setting ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Normalise(string? origin) =>
        Uri.TryCreate(origin?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? $"{uri.Scheme}://{uri.Host}:{uri.Port}".ToLowerInvariant()
            : null;
}
