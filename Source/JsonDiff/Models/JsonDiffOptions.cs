namespace JsonDiff.Models;

/// <param name="SortKeys">
/// Show object members in ordinal key order. When false, the left document's order is kept and members only in
/// the right document are placed after their preceding key there. Array elements matched by
/// <see cref="ArrayKeys"/> follow the same rule: sorted by their key values, or in the left document's order.
/// </param>
public sealed record JsonDiffOptions(bool SortKeys = true)
{
    // Hard-coded until there is UI to configure them. Declared before Default, which reads it during static init.
    public static IReadOnlyList<string> DefaultArrayKeys { get; } = ["Type", "Name"];

    public static JsonDiffOptions Default { get; } = new();

    /// <summary>
    /// Properties that identify an object inside an array. Array elements that have at least one of them are matched
    /// by their values instead of by content and position; the remaining elements are aligned like a text diff.
    /// </summary>
    public IReadOnlyList<string> ArrayKeys { get; init; } = DefaultArrayKeys;
}
