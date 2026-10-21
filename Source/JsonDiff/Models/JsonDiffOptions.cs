using System.Text.Json.Nodes;

namespace JsonDiff.Models;

/// <param name="SortKeys">
/// Show object members in ordinal key order. When false, the left document's order is kept and members only in
/// the right document are placed after their preceding key there. Array elements matched by
/// <see cref="ArrayKeyRules"/> follow the same rule: sorted by their key values, or in the left document's order.
/// </param>
public sealed record JsonDiffOptions(bool SortKeys = true)
{
    public static JsonDiffOptions Default { get; } = new();

    /// <summary>
    /// How to identify objects inside arrays; the first rule that applies to an object wins. Identified elements are
    /// matched by their key values instead of by content and position; the remaining elements are aligned like a
    /// text diff.
    /// </summary>
    public IReadOnlyList<ArrayKeyRule> ArrayKeyRules { get; init; } = [];

    /// <summary>
    /// Object members to leave out of the comparison. They are still shown, as <see cref="DiffLineKind.Ignored"/>,
    /// but never count as a difference and do not affect how array elements are aligned by content.
    /// </summary>
    public IReadOnlyList<IgnoreRule> IgnoreRules { get; init; } = [];

    internal bool IsIgnored(JsonObject obj, string key) => IgnoreRules.Any(rule => rule.Ignores(obj, key));
}
