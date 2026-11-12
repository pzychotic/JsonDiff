using System.IO;
using System.Text.Json;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>In-memory <see cref="IMatchRulesLoader"/>: returns <see cref="Rules"/>, or throws when <see cref="Error"/> is set.</summary>
internal sealed class FakeMatchRulesLoader : IMatchRulesLoader
{
    public MatchRules Rules { get; set; } = MatchRules.Empty;
    public string? Error { get; set; }

    public MatchRules Load() => Error is null ? Rules : throw new JsonException(Error);

    public string FilePath { get; set; } = @"C:\Users\Test\AppData\Roaming\JsonDiff\MatchRules.json";
    public string? CreateError { get; set; }

    public string GetOrCreateFile() => CreateError is null ? FilePath : throw new IOException(CreateError);
}
