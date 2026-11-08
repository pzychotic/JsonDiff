using System.Text.Json.Nodes;

namespace JsonDiff.Models;

/// <summary>
/// Excludes the members named in <see cref="Keys"/> from the comparison, in any object at any depth for which all
/// <see cref="When"/> conditions hold.
/// </summary>
public sealed record IgnoreRule(IReadOnlyList<string> Keys, IReadOnlyList<KeyCondition> When)
{
    internal bool Ignores(JsonObject obj, string key) => Keys.Contains(key) && When.All(c => c.Holds(obj));
}
