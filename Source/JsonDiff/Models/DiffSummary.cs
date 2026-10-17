namespace JsonDiff.Models;

/// <summary>Number of differences by category; unmatched counts are per member/element, not per line.</summary>
public sealed record DiffSummary(int Modified, int TypeChanged, int LeftOnly, int RightOnly)
{
    public int Total => Modified + TypeChanged + LeftOnly + RightOnly;

    public bool IsIdentical => Total == 0;
}
