using JsonDiff.Core.Models;

namespace JsonDiff.Core.Services;

/// <summary>Finds the differences of a <see cref="DiffResult"/> and groups them into hunks with context rows.</summary>
public static class DiffHunks
{
    /// <summary>
    /// The runs of rows where either side is unmatched, modified or type changed. Ignored rows, and the gaps
    /// opposite them, are not differences.
    /// </summary>
    public static IReadOnlyList<DiffBlock> FindBlocks(DiffResult result)
    {
        var blocks = new List<DiffBlock>();
        int? start = null;
        for (var row = 0; row <= result.Left.Count; row++)
        {
            var differs = row < result.Left.Count &&
                          (IsDifference(result.Left[row].Kind) || IsDifference(result.Right[row].Kind));
            if (differs)
                start ??= row;
            else if (start is { } s)
            {
                blocks.Add(new DiffBlock(s, row - 1));
                start = null;
            }
        }
        return blocks;
    }

    /// <summary>
    /// The blocks of <paramref name="result"/>, each widened by up to <paramref name="context"/> rows on both ends;
    /// blocks whose widened ranges touch or overlap share one hunk.
    /// </summary>
    public static IReadOnlyList<DiffHunk> Build(DiffResult result, int context)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(context);

        var count = result.Left.Count;
        var hunks = new List<DiffHunk>();
        (int Start, int End)? current = null;
        foreach (var block in FindBlocks(result))
        {
            var (start, end) = (Math.Max(0, block.Start - context), Math.Min(count - 1, block.End + context));
            if (current is { } c && start <= c.End + 1)
                current = (c.Start, end);
            else
            {
                if (current is { } done)
                    hunks.Add(ToHunk(result, done.Start, done.End));
                current = (start, end);
            }
        }
        if (current is { } last)
            hunks.Add(ToHunk(result, last.Start, last.End));
        return hunks;
    }

    private static DiffHunk ToHunk(DiffResult result, int startRow, int endRow)
    {
        var (leftStart, leftCount) = Range(result.Left, startRow, endRow);
        var (rightStart, rightCount) = Range(result.Right, startRow, endRow);
        return new DiffHunk(startRow, endRow, leftStart, leftCount, rightStart, rightCount);
    }

    private static (int Start, int Count) Range(IReadOnlyList<DiffLine> pane, int startRow, int endRow)
    {
        int? first = null;
        var count = 0;
        for (var row = startRow; row <= endRow; row++)
        {
            if (pane[row].LineNumber is { } number)
            {
                first ??= number;
                count++;
            }
        }
        if (first is { } start)
            return (start, count);

        // No lines on this side: unified diff names the line before the hunk.
        for (var row = startRow - 1; row >= 0; row--)
        {
            if (pane[row].LineNumber is { } before)
                return (before, 0);
        }
        return (0, 0);
    }

    private static bool IsDifference(DiffLineKind kind) =>
        kind is DiffLineKind.Unmatched or DiffLineKind.Modified or DiffLineKind.TypeChanged;
}
