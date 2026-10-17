using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonDiff.Services;

/// <summary>Lenient file loader: accepts comments and trailing commas, rejects duplicate keys.</summary>
public sealed class JsonFileLoader : IJsonFileLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowDuplicateProperties = false,
    };

    public JsonNode? Load(string path) => JsonNode.Parse(File.ReadAllText(path), documentOptions: DocumentOptions);
}
