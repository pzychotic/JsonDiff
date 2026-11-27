using System.IO;
using System.Text.Json;
using JsonDiff.Core.Models;

namespace JsonDiff.Core.Services;

/// <summary>Lenient file loader: accepts comments and trailing commas, rejects duplicate keys.</summary>
public sealed class JsonFileLoader : IJsonFileLoader
{
    internal static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        AllowDuplicateProperties = false,
    };

    public ParsedJson Load(string path) => ParsedJson.Parse(File.ReadAllText(path));
}
