using System.Reflection;
using System.Text.Json;

namespace Ecommerce.Shared.Notifications;

/// <summary>
/// What each notification kind carries in its data (specs/048), read from <c>notification-kinds.json</c> -
/// the one file the storefront's tests read too, so a key renamed on one side fails a test on that side.
/// </summary>
/// <remarks>
/// ⚠️ Checked by the tests, never at run time (plan D2): a wording mismatch that threw inside
/// <see cref="Notifier"/> would roll back the payout or the moderation decision it announces.
/// </remarks>
public static class NotificationContract
{
    public record KindKeys(IReadOnlyList<string> Required, IReadOnlyList<string> Optional);

    private static readonly Lazy<IReadOnlyDictionary<string, KindKeys>> Declared = new(Load);

    public static IReadOnlyDictionary<string, KindKeys> Kinds => Declared.Value;

    /// <summary>Every constant on <see cref="NotificationKind"/>.</summary>
    public static IReadOnlyList<string> KindsInCode =>
        typeof(NotificationKind).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    /// <summary>What is wrong with a notice's data, in words; empty when it matches the declaration.</summary>
    public static IReadOnlyList<string> Problems(string kind, IReadOnlyDictionary<string, string> data)
    {
        if (!Kinds.TryGetValue(kind, out var keys))
        {
            return [$"{kind}: not declared in notification-kinds.json"];
        }

        var missing = keys.Required.Where(k => !data.ContainsKey(k)).Select(k => $"{kind}: missing \"{k}\"");
        var undeclared = data.Keys.Where(k => !keys.Required.Contains(k) && !keys.Optional.Contains(k))
            .Select(k => $"{kind}: sends \"{k}\", which is not declared");
        return [.. missing, .. undeclared];
    }

    private static IReadOnlyDictionary<string, KindKeys> Load()
    {
        using var stream = typeof(NotificationContract).Assembly
            .GetManifestResourceStream("Ecommerce.Shared.Notifications.notification-kinds.json")
            ?? throw new InvalidOperationException("notification-kinds.json is not embedded in Ecommerce.Shared.");
        using var json = JsonDocument.Parse(stream);

        return json.RootElement.GetProperty("kinds").EnumerateObject().ToDictionary(
            kind => kind.Name,
            kind => new KindKeys(Keys(kind.Value, "required"), Keys(kind.Value, "optional")));
    }

    private static List<string> Keys(JsonElement kind, string name) =>
        kind.TryGetProperty(name, out var keys) ? keys.EnumerateArray().Select(k => k.GetString()!).ToList() : [];
}
