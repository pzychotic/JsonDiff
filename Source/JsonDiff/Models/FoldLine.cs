namespace JsonDiff.Models;

/// <summary>
/// A row standing in for <see cref="Count"/> hidden unchanged rows, shown in both panes at the same position.
/// <see cref="Start"/> is the index of the first hidden row in the full diff.
/// </summary>
public sealed record FoldLine(int Start, int Count);
