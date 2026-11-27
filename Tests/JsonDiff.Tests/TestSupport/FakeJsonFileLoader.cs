using System.IO;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>In-memory <see cref="IJsonFileLoader"/>: maps paths to JSON text; unknown paths throw like a missing file.</summary>
internal sealed class FakeJsonFileLoader : IJsonFileLoader
{
    public Dictionary<string, string> Files { get; } = [];

    public ParsedJson Load(string path) =>
        Files.TryGetValue(path, out var text)
            ? ParsedJson.Parse(text)
            : throw new FileNotFoundException("File not found.", path);
}
