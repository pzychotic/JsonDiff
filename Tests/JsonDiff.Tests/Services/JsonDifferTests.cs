using System.Text.Json.Nodes;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class JsonDifferTests
{
    /// <summary>A catch-all rule: objects with a Type or a Name.</summary>
    private static readonly ArrayKeyRule TypeAndName = new([JsonKeyPath.Parse("Type"), JsonKeyPath.Parse("Name")], []);

    private static JsonDiffOptions Keyed(bool sortKeys = true) => new(sortKeys) { ArrayKeyRules = [TypeAndName] };

    private static DiffResult Diff(string left, string right, bool sortKeys = true) =>
        JsonDiffer.Diff(JsonNode.Parse(left), JsonNode.Parse(right), Keyed(sortKeys));

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

    // --- Arrays matched by key ---------------------------------------------

    private static string Item(string type, string name, double z = 0) =>
        $$$"""{"Type":"{{{type}}}","Name":"{{{name}}}","Location":{"x":1,"y":2,"z":{{{z.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}""";

    private static string Items(params string[] items) => $$"""{"Items":[{{string.Join(",", items)}}]}""";

    /// <summary>The values of a key, in the order the pane shows them.</summary>
    private static string[] Shown(IEnumerable<DiffLine> lines, string key) =>
        [.. lines.Where(l => l.Text.TrimStart().StartsWith($"\"{key}\":")).Select(l => l.Text.Trim().TrimEnd(','))];

    private static readonly (string Left, string Right)[] KeyedPairs =
    [
        (Items(Item("Door", "a"), Item("Window", "b")), Items(Item("Window", "b"), Item("Door", "a"))),
        (Items(Item("Door", "a"), Item("Door", "b")), Items(Item("Door", "b"))),
        (Items(Item("Door", "b")), Items(Item("Lamp", "c"), Item("Door", "a"), Item("Door", "b"))),
        ("""[{"Name":"a"},1,{"Name":"b"},2]""", """[3,{"Name":"b"},1,{"other":true}]"""),
        ("""[{"Name":"a"},{"Name":"a","v":1}]""", """[{"Name":"a","v":2}]"""),
    ];

    [TestCaseSource(nameof(KeyedPairs))]
    public void Diff_KeyedArrays_PanesStayAlignedAndValid((string Left, string Right) pair)
    {
        foreach (var sortKeys in new[] { true, false })
        {
            var result = Diff(pair.Left, pair.Right, sortKeys);

            Assert.That(result.Left, Has.Count.EqualTo(result.Right.Count));
            // Reordering must keep the commas right and lose no element.
            Assert.That(SameElements(JsonNode.Parse(Source(result.Left)), JsonNode.Parse(pair.Left)), Is.True);
            Assert.That(SameElements(JsonNode.Parse(Source(result.Right)), JsonNode.Parse(pair.Right)), Is.True);
        }
    }

    /// <summary>Deep equality that ignores the order of array elements.</summary>
    private static bool SameElements(JsonNode? a, JsonNode? b) => (a, b) switch
    {
        (JsonArray x, JsonArray y) => x.Count == y.Count && x.All(i => y.Any(j => SameElements(i, j))),
        (JsonObject x, JsonObject y) => x.Count == y.Count
            && x.All(p => y.TryGetPropertyValue(p.Key, out var v) && SameElements(p.Value, v)),
        _ => JsonNode.DeepEquals(a, b),
    };

    [Test]
    public void Diff_ReorderedItems_AreUnchanged()
    {
        var result = Diff(
            Items(Item("Door", "d1"), Item("Window", "w1"), Item("Door", "d2")),
            Items(Item("Window", "w1"), Item("Door", "d2"), Item("Door", "d1")));

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(0, 0, 0, 0)));
            Assert.That(Kinds(result.Left), Is.All.EqualTo(DiffLineKind.Unchanged));
        });
    }

    [Test]
    public void Diff_KeyedElements_AreSortedByTypeThenName()
    {
        var result = Diff(
            Items(Item("Window", "w1"), Item("Door", "d2"), Item("Door", "d1")),
            Items(Item("Lamp", "l1")));

        // Merge both panes so right-only rows count too.
        var names = result.Left.Zip(result.Right, (l, r) => l.Kind == DiffLineKind.Gap ? r : l);
        Assert.That(Shown(names, "Name"), Is.EqualTo(new[]
        {
            "\"Name\": \"d1\"", "\"Name\": \"d2\"", "\"Name\": \"l1\"", "\"Name\": \"w1\"",
        }));
    }

    [Test]
    public void Diff_EditedItem_IsPairedWithItsNamesake()
    {
        var result = Diff(
            Items(Item("Door", "d1"), Item("Door", "d2", z: 5)),
            Items(Item("Door", "new"), Item("Door", "d2", z: 7), Item("Door", "d1")));

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(1, 0, 0, 1)));
            var modified = result.Right.Where(l => l.Kind == DiffLineKind.Modified).Select(l => l.Text.Trim());
            Assert.That(modified, Is.EqualTo(new[] { "\"z\": 7" }));
        });
    }

    [Test]
    public void Diff_SameNameWithDifferentType_IsNotMatched()
    {
        var result = Diff(Items(Item("Door", "x")), Items(Item("Window", "x")));

        Assert.That(result.Summary, Is.EqualTo(new DiffSummary(0, 0, 1, 1)));
    }

    [Test]
    public void AlignArrays_DuplicateKeys_ArePairedInOrderOfAppearance()
    {
        var left = JsonNode.Parse("""[{"Name":"a","v":1},{"Name":"a","v":2}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"Name":"a","v":3}]""")!.AsArray();

        Assert.That(JsonDiffer.AlignArrays(left, right, Keyed()), Is.EqualTo(new[] { (0, 0), (1, -1) }));
    }

    [Test]
    public void AlignArrays_UnkeyedElements_FollowAndAreAlignedByContent()
    {
        var left = JsonNode.Parse("""[1,{"Name":"b"},2,{"Name":"a"}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"Name":"a"},2,{"Name":"b"}]""")!.AsArray();

        Assert.That(JsonDiffer.AlignArrays(left, right, Keyed()), Is.EqualTo(new[] { (3, 0), (1, 2), (0, -1), (2, 1) }));
    }

    [Test]
    public void AlignArrays_WithoutSorting_KeepsLeftOrderAndInsertsRightOnlyAfterTheirPredecessor()
    {
        var left = JsonNode.Parse("""[{"Name":"c"},{"Name":"a"}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"Name":"new0"},{"Name":"a"},{"Name":"new1"},{"Name":"c"}]""")!.AsArray();

        var rows = JsonDiffer.AlignArrays(left, right, Keyed(sortKeys: false));

        Assert.That(rows, Is.EqualTo(new[] { (-1, 0), (0, 3), (1, 1), (-1, 2) }));
    }

    [Test]
    public void AlignArrays_NumericKeys_SortNumerically()
    {
        var left = JsonNode.Parse("""[{"Name":10},{"Name":9},{"Name":"x"}]""")!.AsArray();

        var rows = JsonDiffer.AlignArrays(left, new JsonArray(), Keyed());

        Assert.That(rows.Select(r => r.Left), Is.EqualTo(new[] { 2, 1, 0 })); // strings before numbers, 9 before 10
    }

    [Test]
    public void AlignArrays_LargeShuffledArray_StillMatchesEveryElement()
    {
        // Far beyond MaxLcsCells, where content alignment falls back to pairing by position.
        const int count = 5000;
        var names = Enumerable.Range(0, count).Select(i => $"f{i:D5}").ToArray();
        var shuffled = names.OrderBy(n => n.GetHashCode()).ToArray();
        var left = new JsonArray([.. names.Select(n => JsonNode.Parse(Item("Door", n)))]);
        var right = new JsonArray([.. shuffled.Select(n => JsonNode.Parse(Item("Door", n)))]);

        var rows = JsonDiffer.AlignArrays(left, right, Keyed());

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(count));
            Assert.That(rows.Select(r => names[r.Left] == shuffled[r.Right]), Is.All.True);
        });
    }

    [Test]
    public void AlignArrays_WithoutArrayKeyRules_AlignsByContent()
    {
        var left = JsonNode.Parse("""[{"Name":"b"},{"Name":"a"}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"Name":"a"},{"Name":"b"}]""")!.AsArray();

        var rows = JsonDiffer.AlignArrays(left, right, JsonDiffOptions.Default);

        Assert.That(rows, Is.EqualTo(new[] { (0, -1), (1, 0), (-1, 1) }));
    }

    [Test]
    public void Diff_ItemSamples_FlagOnlyTheRealEdits()
    {
        static JsonNode? Load(string name) =>
            JsonNode.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Samples", name)));

        var result = JsonDiffer.Diff(Load("items-left.json"), Load("items-right.json"), Keyed());

        // Version, Door_02 z and Locked, Window_01 Glazing, Lamp_01 h, second Spawn y; Smoke_01 removed;
        // Lamp_03 and Window_03 added.
        Assert.That(result.Summary, Is.EqualTo(new DiffSummary(6, 0, 1, 2)));
    }

    // --- Array key rules ----------------------------------------------------

    /// <summary>An item whose identifier is nested in a list of name/value fields.</summary>
    private static string NestedItem(string type, string id, int size = 0) =>
        $$$"""
        {"Type":"{{{type}}}","Fields":[
          {"Name":"Size","Value":"{{{size}}}"},
          {"Name":"Id","Value":"{{{id}}}"}]}
        """;

    private static JsonDiffOptions Rules(params ArrayKeyRule[] rules) => new() { ArrayKeyRules = rules };

    private static readonly ArrayKeyRule NestedIdRule = new(
        [JsonKeyPath.Parse("Type"), JsonKeyPath.Parse("Fields[Name=Id].Value")],
        [new KeyCondition(JsonKeyPath.Parse("Type"), [JsonValue.Create("Sensor"), JsonValue.Create("Camera")])]);

    [Test]
    public void Diff_NestedKey_MatchesReorderedItems()
    {
        var result = JsonDiffer.Diff(
            JsonNode.Parse(Items(NestedItem("Sensor", "a", size: 1), NestedItem("Sensor", "b", size: 2))),
            JsonNode.Parse(Items(NestedItem("Sensor", "b", size: 2), NestedItem("Sensor", "a", size: 1))),
            Rules(NestedIdRule, TypeAndName));

        Assert.That(result.Summary.IsIdentical, Is.True);
    }

    [Test]
    public void Diff_WithoutNestedKey_SameTypeItemsArePairedInOrder()
    {
        var result = JsonDiffer.Diff(
            JsonNode.Parse(Items(NestedItem("Sensor", "a", size: 1), NestedItem("Sensor", "b", size: 2))),
            JsonNode.Parse(Items(NestedItem("Sensor", "b", size: 2), NestedItem("Sensor", "a", size: 1))),
            Keyed());

        // Size and Id differ in both pairs.
        Assert.That(result.Summary, Is.EqualTo(new DiffSummary(4, 0, 0, 0)));
    }

    [Test]
    public void AlignArrays_FirstApplicableRuleWins()
    {
        var left = new JsonArray(JsonNode.Parse(NestedItem("Sensor", "a")), JsonNode.Parse(NestedItem("Lamp", "a")));
        var right = new JsonArray(JsonNode.Parse(NestedItem("Lamp", "z")), JsonNode.Parse(NestedItem("Sensor", "a")));

        // The Lamps fall through to Type/Name, which both lack a Name for, so they still pair.
        var rows = JsonDiffer.AlignArrays(left, right, Rules(NestedIdRule, TypeAndName));

        Assert.That(rows, Is.EquivalentTo(new[] { (0, 1), (1, 0) }));
    }

    [Test]
    public void AlignArrays_ConditionThatDoesNotHold_LeavesElementToLaterRules()
    {
        var left = JsonNode.Parse("""[{"Name":"x"},{"Name":"y"}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"Name":"y"},{"Name":"x"}]""")!.AsArray();

        // NestedIdRule needs a Type, so these stay unkeyed and are aligned by content.
        Assert.That(JsonDiffer.AlignArrays(left, right, Rules(NestedIdRule)),
            Is.EqualTo(new[] { (0, -1), (1, 0), (-1, 1) }));
    }

    [Test]
    public void AlignArrays_KeysOfDifferentRules_NeverMatch()
    {
        var byName = new ArrayKeyRule([JsonKeyPath.Parse("Name")], [new KeyCondition(JsonKeyPath.Parse("Kind"), [JsonValue.Create("a")])]);
        var byId = new ArrayKeyRule([JsonKeyPath.Parse("Id")], []);
        var left = JsonNode.Parse("""[{"Kind":"a","Name":"1"}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"Id":"1"}]""")!.AsArray();

        Assert.That(JsonDiffer.AlignArrays(left, right, Rules(byName, byId)), Is.EqualTo(new[] { (0, -1), (-1, 0) }));
    }

    [Test]
    public void Diff_RulesFromFile_MatchNestedIdentifiers()
    {
        var rules = MatchRulesFileLoader.Parse("""
            {
              "ArrayKeys": [
                { "When": { "Type": ["Sensor", "Camera"] }, "Key": ["Type", "Fields[Name=Id].Value"] },
                { "When": { "Type": "Panel" }, "Key": ["Type", "Fields[Name=Label].Value"] },
                { "Key": ["Type", "Name"] }
              ]
            }
            """);
        static string Item(string type, string field, string value, int size) =>
            $$$"""{"Type":"{{{type}}}","Fields":[{"Name":"{{{field}}}","Value":"{{{value}}}"},{"Name":"Size","Value":{{{size}}}}]}""";

        var result = JsonDiffer.Diff(
            JsonNode.Parse(Items(
                Item("Sensor", "Id", "s1", 1),
                Item("Sensor", "Id", "s2", 2),
                Item("Camera", "Id", "c1", 3),
                Item("Camera", "Id", "c2", 4),
                Item("Panel", "Label", "p1", 5),
                Item("Panel", "Label", "p2", 6))),
            JsonNode.Parse(Items(
                Item("Panel", "Label", "p2", 6),
                Item("Camera", "Id", "c2", 4),
                Item("Sensor", "Id", "s2", 2),
                Item("Panel", "Label", "p1", 5),
                Item("Camera", "Id", "c1", 3),
                Item("Sensor", "Id", "s1", 1))),
            Rules([.. rules.ArrayKeys]));

        Assert.That(result.Summary.IsIdentical, Is.True);
    }

    [Test]
    public void Format_KeyedArray_IsShownInKeyOrder()
    {
        var lines = JsonDiffer.Format(JsonNode.Parse("""[{"Name":"b"},{"Name":"a"}]"""), Keyed());

        Assert.That(Texts(lines), Is.EqualTo(new[]
        {
            "[", "  {", "    \"Name\": \"a\"", "  },", "  {", "    \"Name\": \"b\"", "  }", "]",
        }));
    }

    // --- Ignored keys -------------------------------------------------------

    private static IgnoreRule Ignore(params string[] keys) => new(keys, []);

    private static DiffResult DiffIgnoring(string left, string right, params IgnoreRule[] rules) =>
        JsonDiffer.Diff(JsonNode.Parse(left), JsonNode.Parse(right), Keyed() with { IgnoreRules = rules });

    [TestCaseSource(nameof(Pairs))]
    public void Diff_IgnoringKeys_PanesStayAlignedValidAndNumbered((string Left, string Right) pair)
    {
        var result = DiffIgnoring(pair.Left, pair.Right, Ignore("b", "n", "x"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Left, Has.Count.EqualTo(result.Right.Count));
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(Source(result.Left)), JsonNode.Parse(pair.Left)), Is.True);
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(Source(result.Right)), JsonNode.Parse(pair.Right)), Is.True);
            foreach (var pane in new[] { result.Left, result.Right })
            {
                var numbers = pane.Where(l => l.Kind != DiffLineKind.Gap).Select(l => l.LineNumber).ToArray();
                Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, numbers.Length).Cast<int?>()));
            }
        });
    }

    [Test]
    public void Diff_IgnoredKeyOnOneSide_IsIgnoredWithGapAndNotCounted()
    {
        var result = DiffIgnoring("""{"a":1,"x":true}""", """{"a":1}""", Ignore("x"));

        Assert.Multiple(() =>
        {
            Assert.That(Texts(result.Left), Is.EqualTo(new[] { "{", "  \"a\": 1,", "  \"x\": true", "}" }));
            Assert.That(Kinds(result.Left)[2], Is.EqualTo(DiffLineKind.Ignored));
            Assert.That(Kinds(result.Right)[2], Is.EqualTo(DiffLineKind.Gap));
            Assert.That(result.Summary.IsIdentical, Is.True);
        });
    }

    [Test]
    public void Diff_IgnoredKeyWithDifferentValues_IsIgnoredNotModified()
    {
        var result = DiffIgnoring("""{"a":1,"x":1}""", """{"a":1,"x":2}""", Ignore("x"));

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left)[2], Is.EqualTo(DiffLineKind.Ignored));
            Assert.That(Kinds(result.Right)[2], Is.EqualTo(DiffLineKind.Ignored));
            Assert.That(result.Summary.IsIdentical, Is.True);
        });
    }

    [Test]
    public void Diff_IgnoredObjectValue_IsDimmedAsAWholeBlockPaddedWithGaps()
    {
        var result = DiffIgnoring("""{"x":{"p":1,"q":2}}""", """{"x":0}""", Ignore("x"));

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left).Skip(1).Take(4), Is.All.EqualTo(DiffLineKind.Ignored));
            Assert.That(Kinds(result.Right).Skip(1).Take(4), Is.EqualTo(new[]
            {
                DiffLineKind.Ignored, DiffLineKind.Gap, DiffLineKind.Gap, DiffLineKind.Gap,
            }));
            Assert.That(result.Summary.IsIdentical, Is.True);
        });
    }

    [Test]
    public void Diff_IgnoredKey_AppliesAtAnyDepthAndInsideArrayElements()
    {
        var result = DiffIgnoring(
            """{"x":1,"o":{"x":2,"p":{"x":3}},"list":[{"Name":"a","x":4},[{"x":5}]]}""",
            """{"o":{"p":{}},"list":[{"Name":"a"},[{}]]}""",
            Ignore("x"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Left.Where(l => l.Text.Contains("\"x\"")).Select(l => l.Kind),
                Has.Exactly(5).EqualTo(DiffLineKind.Ignored));
            Assert.That(result.Summary.IsIdentical, Is.True);
        });
    }

    [Test]
    public void Diff_IgnoreRuleWithWhen_OnlyAppliesToMatchingObjects()
    {
        var sensorsOnly = new IgnoreRule(["x"], [new KeyCondition(JsonKeyPath.Parse("Type"), [JsonValue.Create("Sensor")])]);

        var result = DiffIgnoring(
            """[{"Type":"Sensor","x":1},{"Type":"Lamp","x":1}]""",
            """[{"Type":"Sensor"},{"Type":"Lamp"}]""",
            sensorsOnly);

        Assert.Multiple(() =>
        {
            Assert.That(result.Left.Where(l => l.Text.Contains("\"x\"")).Select(l => l.Kind),
                Is.EqualTo(new[] { DiffLineKind.Unmatched, DiffLineKind.Ignored })); // sorted: Lamp, Sensor
            Assert.That(result.Summary, Is.EqualTo(new DiffSummary(0, 0, 1, 0)));
        });
    }

    [Test]
    public void AlignArrays_ElementsDifferingOnlyInIgnoredKeys_MatchByContent()
    {
        var left = JsonNode.Parse("""[{"v":1,"x":1},{"v":2,"x":1}]""")!.AsArray();
        var right = JsonNode.Parse("""[{"v":0},{"v":1},{"v":2}]""")!.AsArray();
        var options = new JsonDiffOptions { IgnoreRules = [Ignore("x")] };

        Assert.That(JsonDiffer.AlignArrays(left, right, options), Is.EqualTo(new[] { (-1, 0), (0, 1), (1, 2) }));
    }

    [Test]
    public void Diff_KeyedArrayWithFlagOnlyOnLeft_HasNoUnmatchedLines()
    {
        var result = DiffIgnoring(
            """{"Items":[{"Flag":true,"Type":"Door","Size":1},{"Flag":true,"Type":"Window","Size":2}]}""",
            """{"Items":[{"Type":"Window","Size":2},{"Type":"Door","Size":1}]}""",
            Ignore("Flag"));

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(result.Left), Has.None.EqualTo(DiffLineKind.Unmatched));
            Assert.That(Kinds(result.Right), Has.None.EqualTo(DiffLineKind.Unmatched));
            Assert.That(result.Left.Where(l => l.Text.Contains("\"Flag\"")).Select(l => l.Kind),
                Is.EqualTo(new[] { DiffLineKind.Ignored, DiffLineKind.Ignored }));
            Assert.That(result.Summary.IsIdentical, Is.True);
        });
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
