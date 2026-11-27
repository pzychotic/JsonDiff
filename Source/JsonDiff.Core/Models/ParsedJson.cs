using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Core.Services;

namespace JsonDiff.Core.Models;

/// <summary>A parsed JSON document (null for a document that is just <c>null</c>) and its source lines.</summary>
public sealed record ParsedJson(JsonNode? Root, SourceLines Lines)
{
    /// <summary>Parses <paramref name="text"/> leniently, like <see cref="JsonFileLoader"/>.</summary>
    /// <exception cref="JsonException">The text is not valid JSON.</exception>
    public static ParsedJson Parse(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var root = JsonNode.Parse(bytes, documentOptions: JsonFileLoader.DocumentOptions);
        return new ParsedJson(root, SourceLines.Read(bytes, root, JsonFileLoader.DocumentOptions));
    }
}
