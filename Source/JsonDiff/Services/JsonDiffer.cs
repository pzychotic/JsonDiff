using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>
/// Structural diff of two JSON documents, rendered as two line-aligned, pretty-printed panes.
/// Object members are matched by key. Array elements identified by one of <see cref="JsonDiffOptions.ArrayKeyRules"/>
/// are matched by their key values; all others by a longest common subsequence over their canonical form.
/// Unmatched elements between two matches are paired by position and diffed recursively, so a small
/// edit inside an array item stays a small edit instead of a removal plus an insertion.
/// Members matched by <see cref="JsonDiffOptions.IgnoreRules"/> are shown but not compared.
/// With <see cref="JsonDiffOptions.CompareNumbers"/>, numbers and numeric strings are compared by value.
/// </summary>
public static class JsonDiffer
{
    private const int IndentSize = 2;

    /// <summary>Above this many LCS table cells, array elements are paired by position only.</summary>
    internal const long MaxLcsCells = 4_000_000;

    /// <summary>What a numeric string may contain: no whitespace, group separators, hex or NaN/Infinity.</summary>
    private const NumberStyles NumberStyle =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

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

        return MergeOrder([.. left.Select(p => p.Key)], right.Select(p => p.Key), left.ContainsKey);
    }

    /// <summary>
    /// The left sequence with the right-only items inserted after the shared item that precedes them on the right.
    /// </summary>
    private static List<string> MergeOrder(List<string> left, IEnumerable<string> right, Func<string, bool> inLeft)
    {
        var head = new List<string>();
        var following = new Dictionary<string, List<string>>();
        var anchor = head;
        foreach (var item in right)
        {
            if (inLeft(item))
            {
                if (!following.TryGetValue(item, out anchor))
                    following[item] = anchor = [];
            }
            else
            {
                anchor.Add(item);
            }
        }

        var merged = new List<string>(head);
        foreach (var item in left)
        {
            merged.Add(item);
            if (following.TryGetValue(item, out var inserted))
                merged.AddRange(inserted);
        }
        return merged;
    }

    /// <summary>
    /// Aligns two arrays as a sequence of (left index, right index) rows; -1 marks an element missing on that side.
    /// Keyed elements (see <see cref="JsonDiffOptions.ArrayKeyRules"/>) come first, matched by key; the rest follow,
    /// aligned by content.
    /// </summary>
    internal static List<(int Left, int Right)> AlignArrays(JsonArray left, JsonArray right, JsonDiffOptions? options = null)
    {
        options ??= JsonDiffOptions.Default;
        var leftKeys = ElementKeys(left, options.ArrayKeyRules);
        var rightKeys = ElementKeys(right, options.ArrayKeyRules);

        var rows = AlignByKey(leftKeys, rightKeys, options.SortKeys);
        int[] leftRest = [.. Enumerable.Range(0, left.Count).Where(i => leftKeys[i] is null)];
        int[] rightRest = [.. Enumerable.Range(0, right.Count).Where(i => rightKeys[i] is null)];
        var contentRows = AlignByContent(
            [.. leftRest.Select(i => Canonical(left[i], options))], [.. rightRest.Select(i => Canonical(right[i], options))]);
        foreach (var (l, r) in contentRows)
            rows.Add((l < 0 ? -1 : leftRest[l], r < 0 ? -1 : rightRest[r]));
        return rows;
    }

    /// <summary>Pairs keyed elements with equal keys, ordered by key or by the left side's order.</summary>
    private static List<(int Left, int Right)> AlignByKey(ElementKey?[] left, ElementKey?[] right, bool sort)
    {
        var leftIndex = IndexByIdentity(left);
        var rightIndex = IndexByIdentity(right);
        if (leftIndex.Count == 0 && rightIndex.Count == 0)
            return [];

        List<string> order;
        if (sort)
        {
            var keys = left.Concat(right).OfType<ElementKey>().DistinctBy(k => k.Identity).ToList();
            keys.Sort();
            order = [.. keys.Select(k => k.Identity)];
        }
        else
        {
            order = MergeOrder([.. leftIndex.Keys], rightIndex.Keys, leftIndex.ContainsKey);
        }

        return [.. order.Select(id => (leftIndex.GetValueOrDefault(id, -1), rightIndex.GetValueOrDefault(id, -1)))];
    }

    /// <summary>Element index by key identity, in array order.</summary>
    private static Dictionary<string, int> IndexByIdentity(ElementKey?[] keys)
    {
        var index = new Dictionary<string, int>();
        for (var i = 0; i < keys.Length; i++)
            if (keys[i] is { } key)
                index.Add(key.Identity, i);
        return index;
    }

    /// <summary>Display order of a single array's elements, consistent with <see cref="AlignArrays"/>.</summary>
    private static IEnumerable<int> DisplayOrder(JsonArray array, JsonDiffOptions options)
    {
        var keys = ElementKeys(array, options.ArrayKeyRules);
        var keyed = Enumerable.Range(0, array.Count).Where(i => keys[i] is not null);
        if (options.SortKeys)
            keyed = keyed.OrderBy(i => keys[i]);
        return keyed.Concat(Enumerable.Range(0, array.Count).Where(i => keys[i] is null));
    }

    /// <summary>The key of each element, or null for elements none of <paramref name="rules"/> applies to.</summary>
    private static ElementKey?[] ElementKeys(JsonArray array, IReadOnlyList<ArrayKeyRule> rules)
    {
        var keys = new ElementKey?[array.Count];
        if (rules.Count == 0)
            return keys;

        var occurrences = new Dictionary<string, int>();
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject obj)
                continue;
            var rule = 0;
            while (rule < rules.Count && !rules[rule].AppliesTo(obj))
                rule++;
            if (rule == rules.Count)
                continue;

            var values = rules[rule].Key
                .Select(path => path.TryResolve(obj, out var value) ? new KeyValue(value) : null)
                .ToArray();
            // Keys of different rules mean different things, so they never match each other.
            var baseIdentity = string.Join("\u0001", values.Select(v => v is null ? "\u0000" : v.Text).Prepend($"{rule}"));
            var occurrence = occurrences.GetValueOrDefault(baseIdentity);
            occurrences[baseIdentity] = occurrence + 1;
            keys[i] = new ElementKey(values, occurrence, $"{baseIdentity}\u0002{occurrence}");
        }
        return keys;
    }

    /// <summary>Aligns two sequences of canonical element texts like a text diff.</summary>
    private static List<(int Left, int Right)> AlignByContent(string[] leftKeys, string[] rightKeys)
    {
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

    /// <summary>
    /// Order-independent text form of a node, used to compare array elements cheaply. Members ignored by
    /// <paramref name="options"/> are left out. With <see cref="JsonDiffOptions.CompareNumbers"/>, numbers are written
    /// by value, so differently written equal numbers align; the tolerance cannot be part of a text form, so elements
    /// that differ only within it are not anchors, but are still paired by position and diffed.
    /// </summary>
    private static string Canonical(JsonNode? node, JsonDiffOptions? options = null)
    {
        var builder = new StringBuilder();
        AppendCanonical(builder, node, options);
        return builder.ToString();
    }

    private static void AppendCanonical(StringBuilder builder, JsonNode? node, JsonDiffOptions? options)
    {
        switch (node)
        {
            case JsonObject obj:
                builder.Append('{');
                foreach (var (key, value) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (options?.IsIgnored(obj, key) == true)
                        continue;
                    builder.Append(KeyText(key)).Append(':');
                    AppendCanonical(builder, value, options);
                    builder.Append(',');
                }
                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                foreach (var item in array)
                {
                    AppendCanonical(builder, item, options);
                    builder.Append(',');
                }
                builder.Append(']');
                break;
            default:
                if (options?.CompareNumbers == true && TryGetNumber(node, out var number))
                    builder.Append(number.Canonical);
                else
                    builder.Append(ValueText(node));
                break;
        }
    }

    /// <summary>The value of a JSON number, or of a string that parses as one; false for anything else.</summary>
    private static bool TryGetNumber(JsonNode? node, out NumericValue number)
    {
        number = default;
        var text = node?.GetValueKind() switch
        {
            JsonValueKind.Number => node!.ToJsonString(),
            JsonValueKind.String => node!.GetValue<string>(),
            _ => null,
        };
        if (text is null)
            return false;

        if (decimal.TryParse(text, NumberStyle, CultureInfo.InvariantCulture, out var exact))
            number = new NumericValue(exact, (double)exact);
        else if (double.TryParse(text, NumberStyle, CultureInfo.InvariantCulture, out var approximate) && double.IsFinite(approximate))
            number = new NumericValue(null, approximate);
        else
            return false;
        return true;
    }

    /// <summary>
    /// A parsed number: exact when it fits a decimal, so a tolerance edge like 1.2345 vs 1.235 at 0.0005 is decided
    /// without binary rounding; otherwise only as a double.
    /// </summary>
    private readonly record struct NumericValue(decimal? Exact, double Approximate)
    {
        public string Canonical => Exact switch
        {
            0m => "0", // also -0
            { } exact => exact.ToString("G29", CultureInfo.InvariantCulture), // drops trailing zeros
            null => Approximate.ToString("R", CultureInfo.InvariantCulture),
        };

        public bool IsWithin(NumericValue other, decimal tolerance)
        {
            if (Exact is { } a && other.Exact is { } b)
            {
                try
                {
                    return Math.Abs(a - b) <= tolerance;
                }
                catch (OverflowException)
                {
                    return false;
                }
            }
            return Math.Abs(Approximate - other.Approximate) <= (double)tolerance;
        }
    }

    /// <summary>One key property's value inside an array element.</summary>
    private sealed class KeyValue(JsonNode? value)
    {
        public JsonValueKind Kind { get; } = KindOf(value);

        public string Text { get; } = Canonical(value);

        public double Number => Kind == JsonValueKind.Number ? double.Parse(Text, CultureInfo.InvariantCulture) : 0;

        public string? String => Kind == JsonValueKind.String ? value!.GetValue<string>() : null;

        public int CompareTo(KeyValue other)
        {
            if (Kind != other.Kind)
                return Kind.CompareTo(other.Kind);
            return Kind switch
            {
                JsonValueKind.Number => Number.CompareTo(other.Number),
                JsonValueKind.String => string.CompareOrdinal(String, other.String),
                _ => string.CompareOrdinal(Text, other.Text),
            };
        }
    }

    /// <summary>
    /// The key values of one array element (null for a missing key) plus how many elements with the same values
    /// came before it in its array, so duplicates are paired in order of appearance.
    /// </summary>
    private sealed class ElementKey(KeyValue?[] values, int occurrence, string identity) : IComparable<ElementKey>
    {
        public string Identity { get; } = identity;

        private KeyValue?[] Values { get; } = values;

        private int Occurrence { get; } = occurrence;

        public int CompareTo(ElementKey? other)
        {
            if (other is null)
                return 1;
            // Rules may have keys of different lengths; a shorter key sorts before a longer one it is a prefix of.
            for (var i = 0; i < Math.Min(Values.Length, other.Values.Length); i++)
            {
                var (a, b) = (Values[i], other.Values[i]);
                // A missing key sorts before any value.
                var result = a is null || b is null ? (a is null ? 0 : 1) - (b is null ? 0 : 1) : a.CompareTo(b);
                if (result != 0)
                    return result;
            }
            if (Values.Length != other.Values.Length)
                return Values.Length.CompareTo(other.Values.Length);
            return Occurrence.CompareTo(other.Occurrence);
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
                var order = DisplayOrder(array, options).ToList();
                for (var i = 0; i < order.Count; i++)
                    AppendFormatted(lines, "", array[order[i]], i < order.Count - 1, depth + 1, options);
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
            // Before the type check, so "1" and 1 compare as numbers too.
            if (options.CompareNumbers && TryGetNumber(left, out var leftNumber) && TryGetNumber(right, out var rightNumber))
            {
                AddValue(prefix, left, right, leftNumber.IsWithin(rightNumber, options.NumberTolerance),
                    leftComma, rightComma, depth);
                return;
            }

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
                    AddValue(prefix, left, right, ValueText(left) == ValueText(right), leftComma, rightComma, depth);
                    break;
            }
        }

        /// <summary>A pair of scalar values on one row, each shown as written.</summary>
        private void AddValue(string prefix, JsonNode? left, JsonNode? right, bool same, bool leftComma, bool rightComma,
            int depth)
        {
            if (!same)
                _modified++;
            AddRow(same ? DiffLineKind.Unchanged : DiffLineKind.Modified,
                Line(depth, prefix + ValueText(left), leftComma), Line(depth, prefix + ValueText(right), rightComma));
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
                if ((inLeft && options.IsIgnored(left, keys[i])) || (inRight && options.IsIgnored(right, keys[i])))
                    AddIgnored(childPrefix, inLeft, leftValue, inRight, rightValue, i < lastLeft, i < lastRight, depth + 1);
                else if (inLeft && inRight)
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

            // Keyed elements may be reordered, so the last element shown is not necessarily the last in the array.
            var rows = AlignArrays(left, right, options);
            var lastLeft = rows.FindLastIndex(r => r.Left >= 0);
            var lastRight = rows.FindLastIndex(r => r.Right >= 0);
            for (var i = 0; i < rows.Count; i++)
            {
                var (li, ri) = rows[i];
                if (li >= 0 && ri >= 0)
                    DiffNode("", left[li], right[ri], i < lastLeft, i < lastRight, depth + 1);
                else if (li >= 0)
                    AddUnmatched(isLeft: true, "", left[li], i < lastLeft, depth + 1);
                else
                    AddUnmatched(isLeft: false, "", right[ri], i < lastRight, depth + 1);
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
            AddSideBySide(DiffLineKind.TypeChanged, leftLines, rightLines);
            _typeChanged++;
        }

        /// <summary>The member on each side that has it, the shorter one padded with gaps; not counted.</summary>
        private void AddIgnored(string prefix, bool inLeft, JsonNode? left, bool inRight, JsonNode? right,
            bool leftComma, bool rightComma, int depth)
        {
            var leftLines = new List<string>();
            var rightLines = new List<string>();
            if (inLeft)
                AppendFormatted(leftLines, prefix, left, leftComma, depth, options);
            if (inRight)
                AppendFormatted(rightLines, prefix, right, rightComma, depth, options);
            AddSideBySide(DiffLineKind.Ignored, leftLines, rightLines);
        }

        private void AddSideBySide(DiffLineKind kind, List<string> leftLines, List<string> rightLines)
        {
            for (var i = 0; i < Math.Max(leftLines.Count, rightLines.Count); i++)
            {
                _left.Add(i < leftLines.Count ? (kind, leftLines[i]) : (DiffLineKind.Gap, ""));
                _right.Add(i < rightLines.Count ? (kind, rightLines[i]) : (DiffLineKind.Gap, ""));
            }
        }

        private void AddRow(DiffLineKind kind, string leftText, string rightText)
        {
            _left.Add((kind, leftText));
            _right.Add((kind, rightText));
        }
    }
}
