using System.IO;
using System.Text;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class SourceLinesTests
{
    private static (DiffLineKind Kind, string Text, int? Display, int? Source)[] Rows(IEnumerable<DiffLine> lines) =>
        [.. lines.Select(l => (l.Kind, l.Text, l.DisplayLine, l.SourceLine))];

    private static int?[] SourceLines(IEnumerable<DiffLine> lines) => [.. lines.Select(l => l.SourceLine)];

    [Test]
    public void Parse_RecordsKeysElementsAndClosingBrackets()
    {
        var parsed = ParsedJson.Parse("{\n  \"a\": 1,\n  \"b\": [\n    true,\n\n    null\n  ]\n}");
        var obj = parsed.Root!.AsObject();
        var array = obj["b"]!.AsArray();

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Lines.Root, Is.EqualTo(1));
            Assert.That(parsed.Lines.Member(obj, "a"), Is.EqualTo(2));
            Assert.That(parsed.Lines.Member(obj, "b"), Is.EqualTo(3));
            Assert.That(parsed.Lines.Element(array, 0), Is.EqualTo(4));
            Assert.That(parsed.Lines.Element(array, 1), Is.EqualTo(6));
            Assert.That(parsed.Lines.Close(array), Is.EqualTo(7));
            Assert.That(parsed.Lines.Close(obj), Is.EqualTo(8));
        });
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\r")]
    public void Parse_CountsEachLineBreakOnce(string newline)
    {
        var parsed = ParsedJson.Parse(string.Join(newline, "[", "1,", "2", "]"));
        var array = parsed.Root!.AsArray();

        Assert.That(new[] { parsed.Lines.Element(array, 0), parsed.Lines.Element(array, 1), parsed.Lines.Close(array) },
            Is.EqualTo(new int?[] { 2, 3, 4 }));
    }

    [Test]
    public void Parse_SkipsCommentsAndNonAsciiText()
    {
        var parsed = ParsedJson.Parse("// head\n{ /* a\n b */ \"grüße\": \"日本\",\n \"x\": 1, }");
        var obj = parsed.Root!.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Lines.Root, Is.EqualTo(2));
            Assert.That(parsed.Lines.Member(obj, "grüße"), Is.EqualTo(3));
            Assert.That(parsed.Lines.Member(obj, "x"), Is.EqualTo(4));
            Assert.That(parsed.Lines.Close(obj), Is.EqualTo(4));
        });
    }

    [Test]
    public void Lookups_OfOtherDocumentsOrNone_AreNull()
    {
        var parsed = ParsedJson.Parse("""{"a":[1]}""");
        var other = ParsedJson.Parse("""{"a":[1]}""").Root!.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Lines.Member(other, "a"), Is.Null);
            Assert.That(parsed.Lines.Close(other), Is.Null);
            Assert.That(Core.Services.SourceLines.None.Root, Is.Null);
        });
    }

    [Test]
    public void Load_IgnoresAByteOrderMark()
    {
        var path = Path.Combine(Path.GetTempPath(), $"JsonDiffTests_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "\n{\"a\": 1}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var parsed = new JsonFileLoader().Load(path);

            Assert.That(parsed.Lines.Member(parsed.Root!.AsObject(), "a"), Is.EqualTo(2));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Format_MinifiedSource_PutsEveryRowOnLineOne()
    {
        var lines = JsonDiffer.Format(ParsedJson.Parse("""{"a":[1,2],"b":{}}"""));

        Assert.Multiple(() =>
        {
            Assert.That(lines.Select(l => l.DisplayLine), Is.EqualTo(Enumerable.Range(1, lines.Count).Cast<int?>()));
            Assert.That(SourceLines(lines), Is.All.EqualTo(1));
        });
    }

    [Test]
    public void Format_SortedKeys_KeepTheirSourceLines()
    {
        var lines = JsonDiffer.Format(ParsedJson.Parse("{\n\"b\": 1,\n\"a\": 2\n}"), new JsonDiffOptions(SortKeys: true));

        Assert.That(Rows(lines), Is.EqualTo(new (DiffLineKind, string, int?, int?)[]
        {
            (DiffLineKind.Unchanged, "{", 1, 1),
            (DiffLineKind.Unchanged, "  \"a\": 2,", 2, 3),
            (DiffLineKind.Unchanged, "  \"b\": 1", 3, 2),
            (DiffLineKind.Unchanged, "}", 4, 4),
        }));
    }

    [Test]
    public void Diff_EachSideTakesItsOwnSourceLines_AndGapsHaveNone()
    {
        var left = ParsedJson.Parse("{\n  \"same\": 1,\n  \"gone\": [\n    1\n  ]\n}");
        var right = ParsedJson.Parse("""{"same":2,"new":true}""");

        var result = JsonDiffer.Diff(left, right, new JsonDiffOptions(SortKeys: false));

        // Rows: {, same, new (right only), gone: [, 1, ], }

        Assert.Multiple(() =>
        {
            Assert.That(SourceLines(result.Left), Is.EqualTo(new int?[] { 1, 2, null, 3, 4, 5, 6 }));
            Assert.That(SourceLines(result.Right), Is.EqualTo(new int?[] { 1, 1, 1, null, null, null, 1 }));
            Assert.That(result.Right.Where(l => l.Kind == DiffLineKind.Gap).Select(l => l.DisplayLine), Is.All.Null);
        });
    }

    [Test]
    public void Diff_TypeChangesAndIgnoredMembers_HaveSourceLines()
    {
        var left = ParsedJson.Parse("{\n\"t\": 1,\n\"i\": 5\n}");
        var right = ParsedJson.Parse("{\n\"t\": [\n2\n],\n\"i\": 6\n}");
        var options = new JsonDiffOptions(SortKeys: false) { IgnoreRules = [new IgnoreRule(["i"], [])] };

        var result = JsonDiffer.Diff(left, right, options);

        Assert.Multiple(() =>
        {
            Assert.That(SourceLines(result.Left), Is.EqualTo(new int?[] { 1, 2, null, null, 3, 4 }));
            Assert.That(SourceLines(result.Right), Is.EqualTo(new int?[] { 1, 2, 3, 4, 5, 6 }));
        });
    }

    [Test]
    public void Diff_KeyedArrayElements_KeepTheirSourceLinesWhenReordered()
    {
        var left = ParsedJson.Parse("[\n{\"Name\":\"b\"},\n{\"Name\":\"a\"}\n]");
        var right = ParsedJson.Parse("[\n{\"Name\":\"a\"},\n{\"Name\":\"b\"}\n]");
        var options = new JsonDiffOptions(SortKeys: true)
        {
            ArrayKeyRules = [new ArrayKeyRule([JsonKeyPath.Parse("Name")], [])],
        };

        var result = JsonDiffer.Diff(left, right, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary.IsIdentical, Is.True);
            // Rows: [, { a's open, "Name", } , { b's ..., ]
            Assert.That(SourceLines(result.Left), Is.EqualTo(new int?[] { 1, 3, 3, 3, 2, 2, 2, 4 }));
            Assert.That(SourceLines(result.Right), Is.EqualTo(new int?[] { 1, 2, 2, 2, 3, 3, 3, 4 }));
        });
    }

    [Test]
    public void Diff_WithoutSourceText_HasNoSourceLines()
    {
        var result = JsonDiffer.Diff(System.Text.Json.Nodes.JsonNode.Parse("[1]"), System.Text.Json.Nodes.JsonNode.Parse("[2]"));

        Assert.That(SourceLines(result.Left.Concat(result.Right)), Is.All.Null);
    }

    [Test]
    public void Hunks_SpanTheLowestToHighestSourceLineOfEachSide()
    {
        var left = ParsedJson.Parse("{\n\"b\": 1,\n\"a\": 2,\n\"c\": 3\n}");
        var right = ParsedJson.Parse("""{"a":2,"b":9}""");

        var hunk = DiffHunks.Build(JsonDiffer.Diff(left, right), context: 0).Single();

        Assert.Multiple(() =>
        {
            // Sorted: a, b, c. The hunk holds b (modified) and c (left only).
            Assert.That(hunk.LeftSource, Is.EqualTo(new LineSpan(2, 4)));
            Assert.That(hunk.RightSource, Is.EqualTo(new LineSpan(1, 1)));
        });
    }
}
