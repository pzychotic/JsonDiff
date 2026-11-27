using JsonDiff.Core.Models;

namespace JsonDiff.Models;

/// <summary>
/// A row standing in for <see cref="Count"/> hidden unchanged rows, shown in both panes at the same position; each pane
/// has its own, with that side's line ranges. <see cref="Start"/> is the index of the first hidden row in the full diff.
/// </summary>
/// <param name="Display">The display lines of this side's hidden rows; null when all of them are gaps.</param>
/// <param name="Source">The lowest and highest source line of this side's hidden rows; null when there are none.</param>
public sealed record FoldLine(int Start, int Count, LineSpan? Display = null, LineSpan? Source = null)
{
    /// <summary>The fold's text with its display line range, e.g. "⋯ 12 unchanged lines (40–51)".</summary>
    public string DisplayLabel => Label(Display);

    /// <summary>The fold's text with its source line range.</summary>
    public string SourceLabel => Label(Source);

    private string Label(LineSpan? span) => span switch
    {
        null => $"⋯ {Count} unchanged lines",
        _ when span.First == span.Last => $"⋯ {Count} unchanged lines (line {span.First})",
        _ => $"⋯ {Count} unchanged lines ({span.First}–{span.Last})",
    };
}
