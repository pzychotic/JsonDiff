using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>A run of adjacent differing rows; <see cref="Start"/> and <see cref="End"/> are inclusive row indices.</summary>
public readonly record struct DiffBlock(int Start, int End);

/// <summary>
/// The rows shown in both panes, each a <see cref="DiffLine"/> or a <see cref="FoldLine"/>. <see cref="FullRows"/>
/// gives the full-diff row index of each shown row (a fold's first hidden row), in ascending order.
/// </summary>
public sealed record FoldedView(IReadOnlyList<object> Left, IReadOnlyList<object> Right, IReadOnlyList<int> FullRows)
{
    /// <summary>The shown row of full row <paramref name="fullRow"/>, or of the first shown row after it.</summary>
    public int ToShownRow(int fullRow)
    {
        var index = BinarySearch(fullRow);
        return index >= 0 ? index : ~index;
    }

    /// <summary>The shown row containing full row <paramref name="fullRow"/>, which may be a fold.</summary>
    public int ToContainingRow(int fullRow)
    {
        var index = BinarySearch(fullRow);
        return index >= 0 ? index : Math.Max(0, ~index - 1);
    }

    private int BinarySearch(int fullRow)
    {
        var (low, high) = (0, FullRows.Count - 1);
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (FullRows[mid] == fullRow)
                return mid;
            if (FullRows[mid] < fullRow)
                low = mid + 1;
            else
                high = mid - 1;
        }
        return ~low;
    }
}

/// <summary>Finds the differences of a <see cref="DiffResult"/> and hides the unchanged rows away from them.</summary>
public static class DiffFolding
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
    /// Every row of the diff, nothing hidden.
    /// </summary>
    public static FoldedView Unfolded(DiffResult result) =>
        new([.. result.Left], [.. result.Right], [.. Enumerable.Range(0, result.Left.Count)]);

    /// <summary>
    /// Keeps the rows within <paramref name="context"/> rows of a block and replaces each other run of rows by a
    /// <see cref="FoldLine"/>, unless the run is a single row or its first row is in <paramref name="expanded"/>.
    /// </summary>
    public static FoldedView Fold(DiffResult result, IReadOnlyList<DiffBlock> blocks, int context,
        IReadOnlySet<int> expanded)
    {
        var count = result.Left.Count;
        var keep = new bool[count];
        foreach (var block in blocks)
        {
            for (var row = Math.Max(0, block.Start - context); row <= Math.Min(count - 1, block.End + context); row++)
                keep[row] = true;
        }

        var (left, right, fullRows) = (new List<object>(), new List<object>(), new List<int>());
        var rowIndex = 0;
        while (rowIndex < count)
        {
            var end = rowIndex;
            while (end < count && keep[end] == keep[rowIndex])
                end++;

            var length = end - rowIndex;
            if (!keep[rowIndex] && length > 1 && !expanded.Contains(rowIndex))
            {
                var fold = new FoldLine(rowIndex, length);
                left.Add(fold);
                right.Add(fold);
                fullRows.Add(rowIndex);
            }
            else
            {
                for (var row = rowIndex; row < end; row++)
                {
                    left.Add(result.Left[row]);
                    right.Add(result.Right[row]);
                    fullRows.Add(row);
                }
            }
            rowIndex = end;
        }
        return new FoldedView(left, right, fullRows);
    }

    private static bool IsDifference(DiffLineKind kind) =>
        kind is DiffLineKind.Unmatched or DiffLineKind.Modified or DiffLineKind.TypeChanged;
}
