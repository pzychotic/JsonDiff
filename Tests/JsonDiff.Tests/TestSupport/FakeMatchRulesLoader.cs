using System.Text.Json;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>In-memory <see cref="IMatchRulesLoader"/>: returns <see cref="Rules"/>, or throws when <see cref="Error"/> is set.</summary>
internal sealed class FakeMatchRulesLoader : IMatchRulesLoader
{
    public MatchRules Rules { get; set; } = MatchRules.Empty;
    public string? Error { get; set; }

    public MatchRules Load() => Error is null ? Rules : throw new JsonException(Error);
}
