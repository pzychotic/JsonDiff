namespace JsonDiff.Models;

/// <summary>Number of differences by category; unmatched counts are per member/element, not per line.</summary>
public sealed record DiffSummary(int Modified, int TypeChanged, int LeftOnly, int RightOnly)
{
    public int Total => Modified + TypeChanged + LeftOnly + RightOnly;

    public bool IsIdentical => Total == 0;

    /// <summary>One sentence for a status line, e.g. "3 differences: 2 modified, 1 only in left."</summary>
    public string Describe()
    {
        if (IsIdentical)
            return "No differences.";

        var parts = new List<string>();
        if (Modified > 0)
            parts.Add($"{Modified} modified");
        if (TypeChanged > 0)
            parts.Add($"{TypeChanged} type changed");
        if (LeftOnly > 0)
            parts.Add($"{LeftOnly} only in left");
        if (RightOnly > 0)
            parts.Add($"{RightOnly} only in right");

        var noun = Total == 1 ? "difference" : "differences";
        return $"{Total} {noun}: {string.Join(", ", parts)}.";
    }
}
