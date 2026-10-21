namespace JsonDiff.Models;

/// <summary>The rules of a match rules file: how to identify array elements, and which members to ignore.</summary>
public sealed record MatchRules(IReadOnlyList<ArrayKeyRule> ArrayKeys, IReadOnlyList<IgnoreRule> IgnoreKeys)
{
    public static MatchRules Empty { get; } = new([], []);
}
