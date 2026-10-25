using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>A colored stretch of the overview bar, in the bar's own units (0 is its top).</summary>
public readonly record struct OverviewMark(DiffLineKind Kind, double Top, double Height);

/// <summary>Maps one pane's rows onto a bar of a given height, for the difference overview.</summary>
public static class OverviewMarks
{
    /// <summary>
    /// One mark per run of adjacent rows of the same drawn kind, scaled to <paramref name="height"/> and at least
    /// <paramref name="minHeight"/> tall so a single row in a big file stays visible. Ordered weakest kind first:
    /// drawn in order, the strongest kind wins where marks of several rows share the same pixels.
    /// </summary>
    public static IReadOnlyList<OverviewMark> Build(IReadOnlyList<object> lines, double height, double minHeight = 2)
    {
        if (lines.Count == 0 || height <= 0)
            return [];

        var scale = height / lines.Count;
        var marks = new List<OverviewMark>();
        var runStart = 0;
        DiffLineKind? runKind = null;
        for (var row = 0; row <= lines.Count; row++)
        {
            var kind = row < lines.Count && lines[row] is DiffLine line && IsDrawn(line.Kind) ? line.Kind : (DiffLineKind?)null;
            if (kind == runKind)
                continue;

            if (runKind is { } drawn)
                marks.Add(Mark(drawn, runStart, row));
            runKind = kind;
            runStart = row;
        }

        // Stable, so marks of one kind stay in row order.
        return [.. marks.OrderBy(mark => Priority(mark.Kind))];

        OverviewMark Mark(DiffLineKind kind, int start, int end)
        {
            var markHeight = Math.Min(height, Math.Max((end - start) * scale, minHeight));
            var top = Math.Min(start * scale, height - markHeight);
            return new OverviewMark(kind, top, markHeight);
        }
    }

    /// <summary>The order in which kinds win a shared pixel; higher wins. Kinds that are not drawn rank lowest.</summary>
    public static int Priority(DiffLineKind kind) => kind switch
    {
        DiffLineKind.TypeChanged => 4,
        DiffLineKind.Modified => 3,
        DiffLineKind.Unmatched => 2,
        DiffLineKind.Ignored => 1,
        _ => 0,
    };

    private static bool IsDrawn(DiffLineKind kind) => Priority(kind) > 0;
}
