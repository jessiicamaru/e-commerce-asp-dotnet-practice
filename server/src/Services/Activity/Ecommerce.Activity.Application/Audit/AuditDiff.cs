using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ecommerce.Activity.Application.Audit;

/// <summary>
/// What changed between two JSON snapshots, field by field (specs/041 research D5).
/// </summary>
/// <remarks>
/// Both sides are flattened to paths - <c>name</c>, <c>prices.VND</c>, <c>options[0].value</c> - and a path
/// is a change when it is on one side only or its values differ. A creation (no before) lists every field
/// as added; a deletion (no after) as removed. Order is the paths', so the same two snapshots always give
/// the same list.
/// </remarks>
public static class AuditDiff
{
    /// <summary>More than this many changed fields is cut short: a log entry is not a database dump.</summary>
    public const int MaxChanges = 200;

    public static List<AuditChange> Compute(string? before, string? after)
    {
        var left = Flatten(before);
        var right = Flatten(after);

        return left.Keys.Union(right.Keys)
            .Order(StringComparer.Ordinal)
            .Select(path => (path, had: left.TryGetValue(path, out var b), b, has: right.TryGetValue(path, out var a), a))
            .Where(x => !x.had || !x.has || Text(x.b) != Text(x.a))
            .Take(MaxChanges)
            .Select(x => new AuditChange(x.path, x.had ? ToElement(x.b) : null, x.has ? ToElement(x.a) : null))
            .ToList();
    }

    private static Dictionary<string, JsonNode?> Flatten(string? json)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        Walk(JsonNode.Parse(json), string.Empty, result);
        return result;
    }

    private static void Walk(JsonNode? node, string path, Dictionary<string, JsonNode?> into)
    {
        switch (node)
        {
            case JsonObject obj when obj.Count > 0:
                foreach (var (key, value) in obj)
                {
                    Walk(value, path.Length == 0 ? key : $"{path}.{key}", into);
                }

                break;
            case JsonArray array when array.Count > 0:
                for (var i = 0; i < array.Count; i++)
                {
                    Walk(array[i], $"{path}[{i}]", into);
                }

                break;
            default:
                // A leaf - or an empty object/array, which is a value in its own right.
                into[path.Length == 0 ? "$" : path] = node?.DeepClone();
                break;
        }
    }

    /// <summary>A JSON null is a null node - compare it as the text "null", not by dereferencing it.</summary>
    private static string Text(JsonNode? node) => node?.ToJsonString() ?? "null";

    private static JsonElement? ToElement(JsonNode? node) =>
        node is null ? JsonDocument.Parse("null").RootElement.Clone() : JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
}
