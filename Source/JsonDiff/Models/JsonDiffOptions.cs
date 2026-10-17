namespace JsonDiff.Models;

/// <param name="SortKeys">
/// Show object members in ordinal key order. When false, the left document's order is kept and members only in
/// the right document are placed after their preceding key there.
/// </param>
public sealed record JsonDiffOptions(bool SortKeys = true)
{
    public static JsonDiffOptions Default { get; } = new();
}
