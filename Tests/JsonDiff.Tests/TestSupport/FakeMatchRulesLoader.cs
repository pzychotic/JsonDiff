using System.Text.Json;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>In-memory <see cref="IMatchRulesLoader"/>: returns <see cref="Rules"/>, or throws when <see cref="Error"/> is set.</summary>
internal sealed class FakeMatchRulesLoader : IMatchRulesLoader
{
    public IReadOnlyList<ArrayKeyRule> Rules { get; set; } = [];
    public string? Error { get; set; }

    public IReadOnlyList<ArrayKeyRule> Load() => Error is null ? Rules : throw new JsonException(Error);
}
