using System.Text.Json.Nodes;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class JsonDifferTests
{
    private static DiffResult Diff(string left, string right, bool sortKeys = true) =>
        JsonDiffer.Diff(JsonNode.Parse(left), JsonNode.Parse(right), new JsonDiffOptions(sortKeys));

    private static string[] Texts(IEnumerable<DiffLine> lines) => [.. lines.Select(l => l.Text)];

    private static DiffLineKind[] Kinds(IEnumerable<DiffLine> lines) => [.. lines.Select(l => l.Kind)];

    /// <summary>The non-gap lines of a pane, joined back together.</summary>
    private static string Source(IEnumerable<DiffLine> lines) =>
        string.Join("\n", lines.Where(l => l.Kind != DiffLineKind.Gap).Select(l => l.Text));

    private static readonly (string Left, string Right)[] Pairs =
    [
        ("""{"a":1}""", """{"a":1}"""),
        ("""{"a":1,"b":2}""", """{"a":1}"""),
        ("""{"a":1}""", """{"a":1,"b":2}"""),
        ("""{"a":1,"z":2}""", """{"z":2}"""),
        ("""{"a":1}""", """{"a":{"x":[1,2]}}"""),
        ("""[1,2,3,4]""", """[1,9,3]"""),
        ("""[1,2,3]""", """[]"""),
        ("""{"a":{}}""", """{"a":{"b":null}}"""),
        ("""[{"id":1,"n":"x"},{"id":2}]""", """[{"id":0},{"id":1,"n":"y"},{"id":2}]"""),
        ("""null""", """{"a":true}"""),
        ("""{"ä":"ö"}""", """{"ä":"ü"}"""),
    ];

    // --- Invariants ---------------------------------------------------------

    [TestCaseSource(nameof(Pairs))]
    public void Diff_PanesAlwaysHaveEqualLength((string Left, string Right) pair)
    {
        var result = Diff(pair.Left, pair.Right);

        Assert.That(result.Left, Has.Count.EqualTo(result.Right.Count));
    }

    [TestCaseSource(nameof(Pairs))]
    public void Diff_EachPaneReparsesToItsDocument((string Left, string Right) pair)
    {
        // Gaps and per-side commas must not break either side's JSON.
        var result = Diff(pair.Left, pair.Right);

        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(Source(result.Left)), JsonNode.Parse(pair.Left)), Is.True);
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(Source(result.Right)), JsonNode.Parse(pair.Right)), Is.True);
        });
    }

    [TestCaseSource(nameof(Pairs))]
    public void Diff_GapsHaveNoLineNumberAndOtherLinesAreNumberedConsecutively((string Left, string Right) pair)
    {
        var result = Diff(pair.Left, pair.Right);

        foreach (var pane in new[] { result.Left, result.Right })
        {
            Assert.That(pane.Where(l => l.Kind == DiffLineKind.Gap).Select(l => l.LineNumber), Is.All.Null);
            var numbers = pane.Where(l => l.Kind != DiffLineKind.Gap).Select(l => l.LineNumber).ToArray();
            Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, numbers.Length).Cast<int?>()));
        }
    }

    // --- Objects ------------------------------------------------------------

    [Test]
    public void Diff_IdenticalDocuments_AreAllUnchanged()
    {
        var json = """{"name":"x","list":[1,{"a":true}],"empty":{}}""";

        var result = Diff(json, json);

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary.IsIdentical, Is.True);
            Assert.That(Kinds(result.Left), Is.All.EqualTo(DiffLineKind.Unchanged));
            Assert.That(Texts(result.Left), Is.EqualTo(Texts(result.Right)));
        });
    }

    [Test]
    public void Diff_KeyMissingOnRight_IsUnmatchedOnLeftWithGapOnRight()
    {
        var result = Diff("""{"a":1,"b":2}""", """{"a":1}""");

        Assert.Multiple(() =>
        {
            Assert.That(Texts(result.Left), Is.EqualTo(new[] { "{", "  \"a\": 1,", "  \"b\": 2", "}" }));
            Assert.That(Texts(result.Right), Is.EqualTo(new[] { "{", "  \"a\": 1", "", "}" }));
            Assert.That(Kinds(result.Left)[2], Is.EqualTo(DiffLineKind.Unmatched));
            Assert.That(Kinds(result.Right)[2], Is.EqualTo(DiffLineKind.Gap));
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(0, 0, 1, 0)));
        });
    }

    [Test]
    public void Diff_NestedObjectOnlyOnLeft_IsUnmatchedAsAWholeBlock()
    {
        var result = Diff("""{"a":{"x":1,"y":2}}""", """{}""");

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left), Is.EqualTo(new[]
            {
                DiffLineKind.Unchanged, DiffLineKind.Unmatched, DiffLineKind.Unmatched,
                DiffLineKind.Unmatched, DiffLineKind.Unmatched, DiffLineKind.Unchanged,
            }));
            Assert.That(Kinds(result.Right).Skip(1).Take(4), Is.All.EqualTo(DiffLineKind.Gap));
            Assert.That(result.Summary.LeftOnly, Is.EqualTo(1));
        });
    }

    [Test]
    public void Diff_DifferentValue_IsModifiedOnBothSides()
    {
        var result = Diff("""{"a":1,"b":"x"}""", """{"a":2,"b":"x"}""");

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left), Is.EqualTo(new[]
                { DiffLineKind.Unchanged, DiffLineKind.Modified, DiffLineKind.Unchanged, DiffLineKind.Unchanged }));
            Assert.That(Kinds(result.Right), Is.EqualTo(Kinds(result.Left)));
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(1, 0, 0, 0)));
        });
    }

    [TestCase("1", "\"1\"")]
    [TestCase("true", "null")]
    [TestCase("1", "[1]")]
    public void Diff_DifferentPrimitiveTypes_AreTypeChanged(string left, string right)
    {
        var result = Diff($$"""{"a":{{left}}}""", $$"""{"a":{{right}}}""");

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left)[1], Is.EqualTo(DiffLineKind.TypeChanged));
            Assert.That(Kinds(result.Right)[1], Is.EqualTo(DiffLineKind.TypeChanged));
            Assert.That(result.Summary.TypeChanged, Is.EqualTo(1));
        });
    }

    [Test]
    public void Diff_TrueVersusFalse_IsModifiedNotTypeChanged()
    {
        var result = Diff("[true]", "[false]");

        Assert.That(result.Summary, Is.EqualTo(new DiffSummary(1, 0, 0, 0)));
    }

    [Test]
    public void Diff_ValueVersusObject_IsPurpleBlockPaddedWithGaps()
    {
        var result = Diff("""{"a":1}""", """{"a":{"x":1,"y":2}}""");

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left), Is.EqualTo(new[]
            {
                DiffLineKind.Unchanged, DiffLineKind.TypeChanged, DiffLineKind.Gap,
                DiffLineKind.Gap, DiffLineKind.Gap, DiffLineKind.Unchanged,
            }));
            Assert.That(Kinds(result.Right).Skip(1).Take(4), Is.All.EqualTo(DiffLineKind.TypeChanged));
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(0, 1, 0, 0)));
        });
    }

    // --- Key order ----------------------------------------------------------

    [Test]
    public void Diff_SortKeys_IgnoresMemberOrder()
    {
        var result = Diff("""{"b":1,"a":2}""", """{"a":2,"b":1}""");

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary.IsIdentical, Is.True);
            Assert.That(Texts(result.Left), Is.EqualTo(new[] { "{", "  \"a\": 2,", "  \"b\": 1", "}" }));
        });
    }

    [Test]
    public void Format_WithoutSorting_KeepsOriginalOrder()
    {
        var lines = JsonDiffer.Format(JsonNode.Parse("""{"b":1,"a":2}"""), new JsonDiffOptions(SortKeys: false));

        Assert.That(Texts(lines), Is.EqualTo(new[] { "{", "  \"b\": 1,", "  \"a\": 2", "}" }));
    }

    [Test]
    public void MergeKeys_WithoutSorting_PlacesRightOnlyKeysAfterTheirPredecessor()
    {
        var left = JsonNode.Parse("""{"c":0,"a":0,"z":0}""")!.AsObject();
        var right = JsonNode.Parse("""{"new0":0,"c":0,"new1":0,"z":0,"new2":0}""")!.AsObject();

        var keys = JsonDiffer.MergeKeys(left, right, sort: false);

        Assert.That(keys, Is.EqualTo(new[] { "new0", "c", "new1", "a", "z", "new2" }));
    }

    // --- Arrays -------------------------------------------------------------

    [Test]
    public void Diff_ArrayInsertion_OnlyMarksTheInsertedElement()
    {
        var result = Diff("[1,2,3,4]", "[1,2,99,3,4]");

        Assert.Multiple(() =>
        {
            Assert.That(Texts(result.Right)[3], Is.EqualTo("  99,"));
            Assert.That(Kinds(result.Right)[3], Is.EqualTo(DiffLineKind.Unmatched));
            Assert.That(Kinds(result.Left)[3], Is.EqualTo(DiffLineKind.Gap));
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(0, 0, 0, 1)));
        });
    }

    [Test]
    public void Diff_ArrayReplacement_PairsElementsAsModified()
    {
        var result = Diff("[1,2,3]", "[1,5,3]");

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left)[2], Is.EqualTo(DiffLineKind.Modified));
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(1, 0, 0, 0)));
        });
    }

    [Test]
    public void Diff_ChangedFieldInsideArrayItem_IsDiffedRecursively()
    {
        var result = Diff(
            """[{"id":1,"name":"a"},{"id":2,"name":"b"}]""",
            """[{"id":0,"name":"new"},{"id":1,"name":"a"},{"id":2,"name":"B"}]""");

        Assert.Multiple(() =>
        {
            // The inserted item is one unmatched block, the edited one only flags its name.
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(1, 0, 0, 1)));
            var modified = result.Right.Where(l => l.Kind == DiffLineKind.Modified).Select(l => l.Text.Trim());
            Assert.That(modified, Is.EqualTo(new[] { "\"name\": \"B\"" }));
        });
    }

    [Test]
    public void AlignArrays_MatchesCommonElementsAndPairsTheRest()
    {
        var left = JsonNode.Parse("[1,2,3,4,5]")!.AsArray();
        var right = JsonNode.Parse("[0,1,3,9,5]")!.AsArray();

        var rows = JsonDiffer.AlignArrays(left, right);

        Assert.That(rows, Is.EqualTo(new[] { (-1, 0), (0, 1), (1, -1), (2, 2), (3, 3), (4, 4) }));
    }

    [Test]
    public void AlignArrays_ObjectsWithReorderedKeys_Match()
    {
        var left = JsonNode.Parse("""[{"a":1,"b":2}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"b":2,"a":1}]""")!.AsArray();

        Assert.That(JsonDiffer.AlignArrays(left, right), Is.EqualTo(new[] { (0, 0) }));
    }

    // --- Formatting ---------------------------------------------------------

    [Test]
    public void Format_KeepsNonAsciiTextReadable()
    {
        var lines = JsonDiffer.Format(JsonNode.Parse("""{"grüße":"日本"}"""));

        Assert.That(lines[1].Text, Is.EqualTo("  \"grüße\": \"日本\""));
    }

    [Test]
    public void Format_NullDocument_IsASingleLine()
    {
        var lines = JsonDiffer.Format(null);

        Assert.That(lines, Is.EqualTo(new[] { new DiffLine(DiffLineKind.Unchanged, "null", 1) }));
    }
}
