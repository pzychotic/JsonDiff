using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonDiff.Models;

/// <summary>
/// The location of a value inside an object, e.g. <c>Fields[Name=Id].Value</c>: property
/// names separated by dots, where <c>[field=value]</c> after an array property selects the element whose
/// <c>field</c> is the string <c>value</c>.
/// </summary>
public sealed class JsonKeyPath
{
    private readonly string _text;
    private readonly Segment[] _segments;

    private JsonKeyPath(string text, Segment[] segments)
    {
        _text = text;
        _segments = segments;
    }

    /// <exception cref="FormatException">The text is not a valid path.</exception>
    public static JsonKeyPath Parse(string text)
    {
        var segments = new List<Segment>();
        var i = 0;
        while (true)
        {
            var end = text.IndexOfAny(['.', '['], i);
            if (end < 0)
                end = text.Length;
            var property = text[i..end];
            if (property.Length == 0)
                throw Invalid("empty property name");

            (string Field, string Value)? filter = null;
            i = end;
            if (i < text.Length && text[i] == '[')
            {
                var close = text.IndexOf(']', i);
                if (close < 0)
                    throw Invalid("missing ']'");
                var condition = text[(i + 1)..close];
                var equals = condition.IndexOf('=');
                if (equals <= 0)
                    throw Invalid("expected [field=value]");
                filter = (condition[..equals], condition[(equals + 1)..]);
                i = close + 1;
            }

            segments.Add(new Segment(property, filter));
            if (i == text.Length)
                return new JsonKeyPath(text, [.. segments]);
            if (text[i] != '.')
                throw Invalid($"unexpected '{text[i]}'");
            i++;
        }

        FormatException Invalid(string reason) => new($"Invalid key path '{text}': {reason}.");
    }

    /// <summary>
    /// The value at this path, or false when a step is missing. When several array elements pass a filter, the first
    /// one in which the rest of the path resolves wins.
    /// </summary>
    public bool TryResolve(JsonNode? node, out JsonNode? value) => TryResolve(node, 0, out value);

    private bool TryResolve(JsonNode? node, int index, out JsonNode? value)
    {
        value = null;
        if (index == _segments.Length)
        {
            value = node;
            return true;
        }

        var segment = _segments[index];
        if (node is not JsonObject obj || !obj.TryGetPropertyValue(segment.Property, out var child))
            return false;
        if (segment.Filter is not { } filter)
            return TryResolve(child, index + 1, out value);
        if (child is not JsonArray array)
            return false;

        foreach (var element in array)
        {
            if (element is JsonObject candidate && candidate.TryGetPropertyValue(filter.Field, out var actual)
                && actual?.GetValueKind() == JsonValueKind.String && actual.GetValue<string>() == filter.Value
                && TryResolve(candidate, index + 1, out value))
                return true;
        }
        return false;
    }

    public override string ToString() => _text;

    private sealed record Segment(string Property, (string Field, string Value)? Filter);
}
