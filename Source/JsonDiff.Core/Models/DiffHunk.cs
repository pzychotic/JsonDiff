namespace JsonDiff.Core.Models;

/// <summary>
/// One or more <see cref="DiffBlock"/>s with the unchanged rows around them. <see cref="StartRow"/> and
/// <see cref="EndRow"/> are inclusive row indices of the <see cref="DiffResult"/>. The display line ranges follow
/// unified diff: a side's start is its first line in the hunk, or the line before the hunk when the side has no lines
/// in it (0 at the start of the file).
/// </summary>
/// <param name="LeftSource">
/// The lowest and highest source line of the left side's rows in the hunk; null when it has none or they are unknown.
/// Source lines need not be contiguous or ascending, so this is a span to look in, not a count of lines.
/// </param>
/// <param name="RightSource">Likewise for the right side.</param>
public sealed record DiffHunk(
    int StartRow, int EndRow, int LeftStart, int LeftCount, int RightStart, int RightCount,
    LineSpan? LeftSource = null, LineSpan? RightSource = null);

/// <summary>An inclusive range of 1-based lines.</summary>
public sealed record LineSpan(int First, int Last);
