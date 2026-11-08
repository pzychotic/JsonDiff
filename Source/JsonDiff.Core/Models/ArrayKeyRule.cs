using System.Text.Json.Nodes;

namespace JsonDiff.Models;

/// <summary>
/// Identifies objects inside arrays by the values at <see cref="Key"/>. The rule applies to an object when all
/// <see cref="When"/> conditions hold; a rule without conditions applies to objects that have at least one key value.
/// </summary>
public sealed record ArrayKeyRule(IReadOnlyList<JsonKeyPath> Key, IReadOnlyList<KeyCondition> When)
{
    internal bool AppliesTo(JsonObject obj) =>
        When.Count > 0 ? When.All(c => c.Holds(obj)) : Key.Any(path => path.TryResolve(obj, out _));
}

/// <summary>Holds for an object whose value at <see cref="Path"/> equals one of <see cref="Values"/>.</summary>
public sealed record KeyCondition(JsonKeyPath Path, IReadOnlyList<JsonNode?> Values)
{
    internal bool Holds(JsonObject obj) =>
        Path.TryResolve(obj, out var actual) && Values.Any(value => JsonNode.DeepEquals(value, actual));
}
