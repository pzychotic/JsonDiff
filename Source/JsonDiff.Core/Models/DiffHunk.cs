namespace JsonDiff.Core.Models;

/// <summary>
/// One or more <see cref="DiffBlock"/>s with the unchanged rows around them. <see cref="StartRow"/> and
/// <see cref="EndRow"/> are inclusive row indices of the <see cref="DiffResult"/>. The line ranges follow unified diff:
/// a side's start is its first line in the hunk, or the line before the hunk when the side has no lines in it (0 at the
/// start of the file).
/// </summary>
public sealed record DiffHunk(int StartRow, int EndRow, int LeftStart, int LeftCount, int RightStart, int RightCount);
