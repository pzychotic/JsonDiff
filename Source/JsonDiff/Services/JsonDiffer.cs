using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>
/// Structural diff of two JSON documents, rendered as two line-aligned, pretty-printed panes.
/// Object members are matched by key; array elements by a longest common subsequence over their canonical
/// form. Unmatched elements between two matches are paired by position and diffed recursively, so a small
/// edit inside an array item stays a small edit instead of a removal plus an insertion.
/// </summary>
public static class JsonDiffer
{
    private const int IndentSize = 2;

    /// <summary>Above this many LCS table cells, array elements are paired by position only.</summary>
    internal const long MaxLcsCells = 4_000_000;

    // Keep non-ASCII text readable instead of \uXXXX escapes.
    private static readonly JsonSerializerOptions TextOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static DiffResult Diff(JsonNode? left, JsonNode? right, JsonDiffOptions? options = null)
    {
        var writer = new Writer(options ?? JsonDiffOptions.Default);
        writer.DiffNode("", left, right, leftComma: false, rightComma: false, depth: 0);
        return writer.ToResult();
    }

    /// <summary>Pretty-prints a single document, every line <see cref="DiffLineKind.Unchanged"/>.</summary>
    public static IReadOnlyList<DiffLine> Format(JsonNode? node, JsonDiffOptions? options = null)
    {
        var lines = new List<string>();
        AppendFormatted(lines, "", node, comma: false, depth: 0, options ?? JsonDiffOptions.Default);
        return Number(lines.Select(text => (DiffLineKind.Unchanged, text)));
    }

    /// <summary>Union of both objects' keys in display order (see <see cref="JsonDiffOptions.SortKeys"/>).</summary>
    internal static List<string> MergeKeys(JsonObject left, JsonObject right, bool sort)
    {
        if (sort)
            return [.. left.Select(p => p.Key).Union(right.Select(p => p.Key)).Order(StringComparer.Ordinal)];

        var merged = left.Select(p => p.Key).ToList();
        var anchor = -1;
        foreach (var (key, _) in right)
        {
            if (left.ContainsKey(key))
                anchor = merged.IndexOf(key);
            else
                merged.Insert(++anchor, key);
        }
        return merged;
    }

    /// <summary>
    /// Aligns two arrays as a sequence of (left index, right index) rows; -1 marks an element missing on that side.
    /// </summary>
    internal static List<(int Left, int Right)> AlignArrays(JsonArray left, JsonArray right)
    {
        var leftKeys = left.Select(Canonical).ToArray();
        var rightKeys = right.Select(Canonical).ToArray();
        int n = leftKeys.Length, m = rightKeys.Length;

        // Trimming the common head and tail keeps the quadratic LCS small for typical edits.
        var head = 0;
        while (head < n && head < m && leftKeys[head] == rightKeys[head])
            head++;
        var tail = 0;
        while (tail < n - head && tail < m - head && leftKeys[n - 1 - tail] == rightKeys[m - 1 - tail])
            tail++;

        var matches = new List<(int Left, int Right)>();
        for (var i = 0; i < head; i++)
            matches.Add((i, i));
        matches.AddRange(LcsMatches(leftKeys, rightKeys, head, n - tail, head, m - tail));
        for (var k = tail; k > 0; k--)
            matches.Add((n - k, m - k));

        var rows = new List<(int Left, int Right)>();
        int li = 0, ri = 0;
        foreach (var (ml, mr) in matches.Append((n, m)))
        {
            while (li < ml && ri < mr)
                rows.Add((li++, ri++));
            while (li < ml)
                rows.Add((li++, -1));
            while (ri < mr)
                rows.Add((-1, ri++));
            if (ml < n) // not the sentinel
                rows.Add((li++, ri++));
        }
        return rows;
    }

    private static List<(int Left, int Right)> LcsMatches(string[] left, string[] right, int l0, int l1, int r0, int r1)
    {
        int n = l1 - l0, m = r1 - r0;
        if (n == 0 || m == 0 || (long)n * m > MaxLcsCells)
            return [];

        // table[i, j] = LCS length of left[l0 + i ..] and right[r0 + j ..]
        var table = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                table[i, j] = left[l0 + i] == right[r0 + j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);

        var matches = new List<(int, int)>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (left[l0 + x] == right[r0 + y])
                matches.Add((l0 + x++, r0 + y++));
            else if (table[x + 1, y] >= table[x, y + 1])
                x++;
            else
                y++;
        }
        return matches;
    }

    /// <summary>Order-independent text form of a node, used to compare array elements cheaply.</summary>
    private static string Canonical(JsonNode? node)
    {
        var builder = new StringBuilder();
        AppendCanonical(builder, node);
        return builder.ToString();
    }

    private static void AppendCanonical(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                builder.Append('{');
                foreach (var (key, value) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    builder.Append(KeyText(key)).Append(':');
                    AppendCanonical(builder, value);
                    builder.Append(',');
                }
                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                foreach (var item in array)
                {
                    AppendCanonical(builder, item);
                    builder.Append(',');
                }
                builder.Append(']');
                break;
            default:
                builder.Append(ValueText(node));
                break;
        }
    }

    private static void AppendFormatted(List<string> lines, string prefix, JsonNode? node, bool comma, int depth,
        JsonDiffOptions options)
    {
        switch (node)
        {
            case JsonObject obj when obj.Count > 0:
                lines.Add(Line(depth, prefix + "{", false));
                List<KeyValuePair<string, JsonNode?>> members =
                    options.SortKeys ? [.. obj.OrderBy(p => p.Key, StringComparer.Ordinal)] : [.. obj];
                for (var i = 0; i < members.Count; i++)
                    AppendFormatted(lines, Prefix(members[i].Key), members[i].Value, i < members.Count - 1, depth + 1, options);
                lines.Add(Line(depth, "}", comma));
                break;
            case JsonObject:
                lines.Add(Line(depth, prefix + "{}", comma));
                break;
            case JsonArray array when array.Count > 0:
                lines.Add(Line(depth, prefix + "[", false));
                for (var i = 0; i < array.Count; i++)
                    AppendFormatted(lines, "", array[i], i < array.Count - 1, depth + 1, options);
                lines.Add(Line(depth, "]", comma));
                break;
            case JsonArray:
                lines.Add(Line(depth, prefix + "[]", comma));
                break;
            default:
                lines.Add(Line(depth, prefix + ValueText(node), comma));
                break;
        }
    }

    /// <summary>JSON type of a node, with true and false folded into one kind.</summary>
    private static JsonValueKind KindOf(JsonNode? node) => node?.GetValueKind() switch
    {
        null => JsonValueKind.Null,
        JsonValueKind.False => JsonValueKind.True,
        var kind => kind.Value,
    };

    private static string ValueText(JsonNode? node) => node?.ToJsonString(TextOptions) ?? "null";

    private static string KeyText(string key) => JsonSerializer.Serialize(key, TextOptions);

    private static string Prefix(string key) => KeyText(key) + ": ";

    private static string Line(int depth, string text, bool comma) =>
        string.Concat(new string(' ', depth * IndentSize), text, comma ? "," : "");

    private static DiffLine[] Number(IEnumerable<(DiffLineKind Kind, string Text)> rows)
    {
        var lineNumber = 0;
        return [.. rows.Select(r => new DiffLine(r.Kind, r.Text, r.Kind == DiffLineKind.Gap ? null : ++lineNumber))];
    }

    /// <summary>Accumulates the rows of both panes while walking the two documents in lockstep.</summary>
    private sealed class Writer(JsonDiffOptions options)
    {
        private readonly List<(DiffLineKind Kind, string Text)> _left = [];
        private readonly List<(DiffLineKind Kind, string Text)> _right = [];
        private int _modified, _typeChanged, _leftOnly, _rightOnly;

        public DiffResult ToResult() =>
            new(Number(_left), Number(_right), new DiffSummary(_modified, _typeChanged, _leftOnly, _rightOnly));

        public void DiffNode(string prefix, JsonNode? left, JsonNode? right, bool leftComma, bool rightComma, int depth)
        {
            var kind = KindOf(left);
            if (kind != KindOf(right))
            {
                AddTypeChange(prefix, left, right, leftComma, rightComma, depth);
                return;
            }

            switch (kind)
            {
                case JsonValueKind.Object:
                    DiffObject(prefix, left!.AsObject(), right!.AsObject(), leftComma, rightComma, depth);
                    break;
                case JsonValueKind.Array:
                    DiffArray(prefix, left!.AsArray(), right!.AsArray(), leftComma, rightComma, depth);
                    break;
                default:
                    var leftText = ValueText(left);
                    var rightText = ValueText(right);
                    var same = leftText == rightText;
                    if (!same)
                        _modified++;
                    AddRow(same ? DiffLineKind.Unchanged : DiffLineKind.Modified,
                        Line(depth, prefix + leftText, leftComma), Line(depth, prefix + rightText, rightComma));
                    break;
            }
        }

        private void DiffObject(string prefix, JsonObject left, JsonObject right, bool leftComma, bool rightComma, int depth)
        {
            if (left.Count == 0 && right.Count == 0)
            {
                AddRow(DiffLineKind.Unchanged, Line(depth, prefix + "{}", leftComma), Line(depth, prefix + "{}", rightComma));
                return;
            }

            var open = Line(depth, prefix + "{", false);
            AddRow(DiffLineKind.Unchanged, open, open);

            var keys = MergeKeys(left, right, options.SortKeys);
            var lastLeft = keys.FindLastIndex(left.ContainsKey);
            var lastRight = keys.FindLastIndex(right.ContainsKey);
            for (var i = 0; i < keys.Count; i++)
            {
                var childPrefix = Prefix(keys[i]);
                var inLeft = left.TryGetPropertyValue(keys[i], out var leftValue);
                var inRight = right.TryGetPropertyValue(keys[i], out var rightValue);
                if (inLeft && inRight)
                    DiffNode(childPrefix, leftValue, rightValue, i < lastLeft, i < lastRight, depth + 1);
                else if (inLeft)
                    AddUnmatched(isLeft: true, childPrefix, leftValue, i < lastLeft, depth + 1);
                else
                    AddUnmatched(isLeft: false, childPrefix, rightValue, i < lastRight, depth + 1);
            }

            AddRow(DiffLineKind.Unchanged, Line(depth, "}", leftComma), Line(depth, "}", rightComma));
        }

        private void DiffArray(string prefix, JsonArray left, JsonArray right, bool leftComma, bool rightComma, int depth)
        {
            if (left.Count == 0 && right.Count == 0)
            {
                AddRow(DiffLineKind.Unchanged, Line(depth, prefix + "[]", leftComma), Line(depth, prefix + "[]", rightComma));
                return;
            }

            var open = Line(depth, prefix + "[", false);
            AddRow(DiffLineKind.Unchanged, open, open);

            foreach (var (li, ri) in AlignArrays(left, right))
            {
                if (li >= 0 && ri >= 0)
                    DiffNode("", left[li], right[ri], li < left.Count - 1, ri < right.Count - 1, depth + 1);
                else if (li >= 0)
                    AddUnmatched(isLeft: true, "", left[li], li < left.Count - 1, depth + 1);
                else
                    AddUnmatched(isLeft: false, "", right[ri], ri < right.Count - 1, depth + 1);
            }

            AddRow(DiffLineKind.Unchanged, Line(depth, "]", leftComma), Line(depth, "]", rightComma));
        }

        private void AddUnmatched(bool isLeft, string prefix, JsonNode? node, bool comma, int depth)
        {
            var lines = new List<string>();
            AppendFormatted(lines, prefix, node, comma, depth, options);
            var (own, other) = isLeft ? (_left, _right) : (_right, _left);
            foreach (var text in lines)
            {
                own.Add((DiffLineKind.Unmatched, text));
                other.Add((DiffLineKind.Gap, ""));
            }

            if (isLeft)
                _leftOnly++;
            else
                _rightOnly++;
        }

        /// <summary>Both values in full, the shorter one padded with gaps.</summary>
        private void AddTypeChange(string prefix, JsonNode? left, JsonNode? right, bool leftComma, bool rightComma, int depth)
        {
            var leftLines = new List<string>();
            var rightLines = new List<string>();
            AppendFormatted(leftLines, prefix, left, leftComma, depth, options);
            AppendFormatted(rightLines, prefix, right, rightComma, depth, options);

            for (var i = 0; i < Math.Max(leftLines.Count, rightLines.Count); i++)
            {
                _left.Add(i < leftLines.Count ? (DiffLineKind.TypeChanged, leftLines[i]) : (DiffLineKind.Gap, ""));
                _right.Add(i < rightLines.Count ? (DiffLineKind.TypeChanged, rightLines[i]) : (DiffLineKind.Gap, ""));
            }
            _typeChanged++;
        }

        private void AddRow(DiffLineKind kind, string leftText, string rightText)
        {
            _left.Add((kind, leftText));
            _right.Add((kind, rightText));
        }
    }
}
