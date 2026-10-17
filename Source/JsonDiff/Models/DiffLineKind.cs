namespace JsonDiff.Models;

/// <summary>How a line in one pane relates to the line at the same row in the other pane.</summary>
public enum DiffLineKind
{
    /// <summary>Same content on both sides.</summary>
    Unchanged,

    /// <summary>Present only on this side; the other side shows a <see cref="Gap"/> at this row.</summary>
    Unmatched,

    /// <summary>Filler row keeping both panes aligned; has no source line.</summary>
    Gap,

    /// <summary>Same key/position and type on both sides, but a different value.</summary>
    Modified,

    /// <summary>Same key/position on both sides, but a different JSON type (e.g. a number vs. an object).</summary>
    TypeChanged,
}
