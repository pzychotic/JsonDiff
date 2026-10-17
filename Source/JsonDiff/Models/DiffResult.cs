namespace JsonDiff.Models;

/// <summary>Both panes' lines, always of equal length so row <c>i</c> of one lines up with row <c>i</c> of the other.</summary>
public sealed record DiffResult(IReadOnlyList<DiffLine> Left, IReadOnlyList<DiffLine> Right, DiffSummary Summary);
