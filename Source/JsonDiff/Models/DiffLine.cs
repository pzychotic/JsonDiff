namespace JsonDiff.Models;

/// <summary>One row of a pane: pretty-printed JSON text and its 1-based line number, or a gap without either.</summary>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? LineNumber);
