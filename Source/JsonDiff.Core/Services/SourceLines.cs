using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonDiff.Core.Services;

/// <summary>
/// Where the parts of a parsed document stand in its source text, by 1-based line: the root value, each object
/// member's key, each array element's value, and each object's or array's closing bracket. <c>\r\n</c>, <c>\n</c>
/// and a lone <c>\r</c> each end a line. Lookups for nodes of another document, or with <see cref="None"/>, give null.
/// </summary>
public sealed class SourceLines
{
    /// <summary>No source text: every lookup gives null.</summary>
    public static readonly SourceLines None = new(null, new(ReferenceEqualityComparer.Instance));

    private readonly Dictionary<JsonNode, Container> _containers;

    private SourceLines(int? root, Dictionary<JsonNode, Container> containers)
    {
        Root = root;
        _containers = containers;
    }

    /// <summary>The line where the root value starts.</summary>
    public int? Root { get; }

    /// <summary>The line of the key of <paramref name="key"/> in <paramref name="obj"/>.</summary>
    public int? Member(JsonObject obj, string key) =>
        _containers.TryGetValue(obj, out var container) && obj.IndexOf(key) is var index and >= 0
            ? container.Children[index]
            : null;

    /// <summary>The line where element <paramref name="index"/> of <paramref name="array"/> starts.</summary>
    public int? Element(JsonArray array, int index) =>
        _containers.TryGetValue(array, out var container) ? container.Children[index] : null;

    /// <summary>The line of the closing bracket of an object or array.</summary>
    public int? Close(JsonNode container) =>
        _containers.TryGetValue(container, out var lines) ? lines.Close : null;

    /// <summary>
    /// The lines of <paramref name="root"/>, which must have been parsed from <paramref name="utf8Json"/> with
    /// matching <paramref name="options"/>.
    /// </summary>
    public static SourceLines Read(ReadOnlySpan<byte> utf8Json, JsonNode? root, JsonDocumentOptions options)
    {
        var lineStarts = LineStarts(utf8Json);
        var containers = new Dictionary<JsonNode, Container>(ReferenceEqualityComparer.Instance);
        var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions
        {
            CommentHandling = options.CommentHandling,
            AllowTrailingCommas = options.AllowTrailingCommas,
            MaxDepth = options.MaxDepth,
        });
        if (!reader.Read())
            return None;
        var rootLine = Walk(ref reader, root, lineStarts, containers);
        return new SourceLines(rootLine, containers);
    }

    /// <summary>
    /// Records the lines of the value the reader is on, which <paramref name="node"/> was parsed from, and leaves the
    /// reader on the value's last token. Returns the line the value starts on.
    /// </summary>
    private static int Walk(ref Utf8JsonReader reader, JsonNode? node, List<long> lineStarts,
        Dictionary<JsonNode, Container> containers)
    {
        var start = LineAt(lineStarts, reader.TokenStartIndex);
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
            {
                var obj = (JsonObject)node!;
                var children = new int[obj.Count];
                for (var i = 0; reader.Read() && reader.TokenType != JsonTokenType.EndObject; i++)
                {
                    children[i] = LineAt(lineStarts, reader.TokenStartIndex);
                    reader.Read();
                    Walk(ref reader, obj.GetAt(i).Value, lineStarts, containers);
                }
                containers[obj] = new Container(children, LineAt(lineStarts, reader.TokenStartIndex));
                break;
            }
            case JsonTokenType.StartArray:
            {
                var array = (JsonArray)node!;
                var children = new int[array.Count];
                for (var i = 0; reader.Read() && reader.TokenType != JsonTokenType.EndArray; i++)
                    children[i] = Walk(ref reader, array[i], lineStarts, containers);
                containers[array] = new Container(children, LineAt(lineStarts, reader.TokenStartIndex));
                break;
            }
        }
        return start;
    }

    /// <summary>The byte offset of each line's first byte, ascending.</summary>
    private static List<long> LineStarts(ReadOnlySpan<byte> text)
    {
        var starts = new List<long> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' || (text[i] == '\r' && (i + 1 == text.Length || text[i + 1] != '\n')))
                starts.Add(i + 1);
        }
        return starts;
    }

    private static int LineAt(List<long> lineStarts, long offset)
    {
        var index = lineStarts.BinarySearch(offset);
        return (index >= 0 ? index : ~index - 1) + 1;
    }

    /// <summary>The lines of an object's keys or an array's elements, in order, and of the closing bracket.</summary>
    private sealed record Container(int[] Children, int Close);
}
