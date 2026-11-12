namespace JsonDiff.Core.Models;

/// <summary>A run of adjacent differing rows; <see cref="Start"/> and <see cref="End"/> are inclusive row indices.</summary>
public readonly record struct DiffBlock(int Start, int End);
