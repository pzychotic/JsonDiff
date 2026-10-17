using System.IO;
using System.Text.Json.Nodes;
using JsonDiff.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>In-memory <see cref="IJsonFileLoader"/>: maps paths to JSON text; unknown paths throw like a missing file.</summary>
internal sealed class FakeJsonFileLoader : IJsonFileLoader
{
    public Dictionary<string, string> Files { get; } = [];

    public JsonNode? Load(string path) =>
        Files.TryGetValue(path, out var text)
            ? JsonNode.Parse(text)
            : throw new FileNotFoundException("File not found.", path);
}
