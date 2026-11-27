namespace JsonDiff.Core.Models;

/// <summary>
/// One row of a pane: pretty-printed JSON text with its line numbers, or a gap without either.
/// </summary>
/// <param name="DisplayLine">The 1-based line in the pretty-printed pane, counting only rows that are not gaps.</param>
/// <param name="SourceLine">
/// The 1-based line in the source file that the row's text comes from: a member's key, an element's value, or a
/// closing bracket. Rows may share one, and with reordering they need not ascend. Null for gaps and when the source
/// text is unknown.
/// </param>
public sealed record DiffLine(DiffLineKind Kind, string Text, int? DisplayLine, int? SourceLine = null);
