namespace JsonDiff.Models;

/// <summary>A highlighted stretch of a row's text; <see cref="IsCurrent"/> marks the search hit navigated to.</summary>
public readonly record struct TextHighlight(int Start, int Length, bool IsCurrent);

/// <summary>
/// The search hits of one pane, by row. Rows are looked up by reference: equal rows of different lines are separate
/// records. Never changed once built, so a new instance tells the view to redraw.
/// </summary>
public sealed class SearchHighlights
{
    private readonly Dictionary<DiffLine, List<TextHighlight>> _byLine = new(ReferenceEqualityComparer.Instance);

    public SearchHighlights(IEnumerable<(DiffLine Line, TextHighlight Highlight)> highlights)
    {
        foreach (var (line, highlight) in highlights)
        {
            if (!_byLine.TryGetValue(line, out var list))
                _byLine[line] = list = [];
            list.Add(highlight);
        }
    }

    /// <summary>The highlights of <paramref name="line"/>, in text order; empty if it has none.</summary>
    public IReadOnlyList<TextHighlight> For(DiffLine line) =>
        _byLine.TryGetValue(line, out var list) ? list : [];
}
