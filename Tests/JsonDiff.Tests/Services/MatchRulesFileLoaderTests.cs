using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class MatchRulesFileLoaderTests
{
    private string _directory = "";

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"JsonDiffTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private string WriteFile(string name, string json)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);
        return path;
    }

    [Test]
    public void Parse_ReadsKeysAndConditions()
    {
        var rules = MatchRulesFileLoader.Parse("""
            // comments and trailing commas are fine
            {
              "ArrayKeys": [
                { "When": { "Type": ["A", "B"], "Flag": true }, "Key": ["Type", "Fields[Name=Id].Value"], },
                { "Key": ["Name"] },
              ],
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(rules.ArrayKeys, Has.Count.EqualTo(2));
            Assert.That(rules.ArrayKeys[0].Key.Select(k => k.ToString()),
                Is.EqualTo(new[] { "Type", "Fields[Name=Id].Value" }));
            Assert.That(rules.ArrayKeys[0].When.Select(c => c.Path.ToString()), Is.EqualTo(new[] { "Type", "Flag" }));
            Assert.That(rules.ArrayKeys[0].When[0].Values.Select(v => v!.GetValue<string>()), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(rules.ArrayKeys[0].When[1].Values.Single()!.GetValue<bool>(), Is.True);
            Assert.That(rules.ArrayKeys[1].When, Is.Empty);
        });
    }

    [Test]
    public void Parse_ConditionsSelectTheRule()
    {
        var rules = MatchRulesFileLoader.Parse("""{"ArrayKeys":[{"When":{"T":["A","B"]},"Key":["T"]}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(rules.ArrayKeys[0].AppliesTo(JsonNode.Parse("""{"T":"B"}""")!.AsObject()), Is.True);
            Assert.That(rules.ArrayKeys[0].AppliesTo(JsonNode.Parse("""{"T":"C"}""")!.AsObject()), Is.False);
            Assert.That(rules.ArrayKeys[0].AppliesTo(JsonNode.Parse("""{"X":"A"}""")!.AsObject()), Is.False);
        });
    }

    [Test]
    public void Parse_WithoutSections_IsEmpty()
    {
        var rules = MatchRulesFileLoader.Parse("{}");

        Assert.Multiple(() =>
        {
            Assert.That(rules.ArrayKeys, Is.Empty);
            Assert.That(rules.IgnoreKeys, Is.Empty);
        });
    }

    [Test]
    public void Parse_ReadsIgnoreRules()
    {
        var rules = MatchRulesFileLoader.Parse("""
            {
              "IgnoreKeys": [
                "Timestamp",
                { "When": { "Type": ["A", "B"] }, "Keys": ["Revision", "Author"], },
              ],
            }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(rules.ArrayKeys, Is.Empty);
            Assert.That(rules.IgnoreKeys, Has.Count.EqualTo(2));
            Assert.That(rules.IgnoreKeys[0].Keys, Is.EqualTo(new[] { "Timestamp" }));
            Assert.That(rules.IgnoreKeys[0].When, Is.Empty);
            Assert.That(rules.IgnoreKeys[1].Keys, Is.EqualTo(new[] { "Revision", "Author" }));
            Assert.That(rules.IgnoreKeys[1].When.Single().Path.ToString(), Is.EqualTo("Type"));
            Assert.That(rules.IgnoreKeys[1].When.Single().Values.Select(v => v!.GetValue<string>()),
                Is.EqualTo(new[] { "A", "B" }));
        });
    }

    [Test]
    public void Parse_IgnoreConditionsSelectTheObjects()
    {
        var rule = MatchRulesFileLoader.Parse("""{"IgnoreKeys":[{"When":{"T":"A"},"Keys":["x"]}]}""").IgnoreKeys[0];

        Assert.Multiple(() =>
        {
            Assert.That(rule.Ignores(JsonNode.Parse("""{"T":"A","x":1}""")!.AsObject(), "x"), Is.True);
            Assert.That(rule.Ignores(JsonNode.Parse("""{"T":"A","y":1}""")!.AsObject(), "y"), Is.False);
            Assert.That(rule.Ignores(JsonNode.Parse("""{"T":"B","x":1}""")!.AsObject(), "x"), Is.False);
        });
    }

    [TestCase("[]", "object")]
    [TestCase("""{"ArrayKeys":{}}""", "array")]
    [TestCase("""{"ArrayKeys":[1]}""", "ArrayKeys[0] must be an object")]
    [TestCase("""{"ArrayKeys":[{}]}""", "ArrayKeys[0].Key")]
    [TestCase("""{"ArrayKeys":[{"Key":[]}]}""", "ArrayKeys[0].Key")]
    [TestCase("""{"ArrayKeys":[{"Key":[1]}]}""", "ArrayKeys[0].Key")]
    [TestCase("""{"ArrayKeys":[{"Key":["a"]},{"Keys":["a"]}]}""", "ArrayKeys[1] has an unknown property \"Keys\"")]
    [TestCase("""{"ArrayKeys":[{"Key":["a[b"]}]}""", "ArrayKeys[0].Key: Invalid key path 'a[b'")]
    [TestCase("""{"ArrayKeys":[{"Key":["a"],"When":["T"]}]}""", "ArrayKeys[0].When")]
    [TestCase("""{"ArrayKeys":[{"Key":["a"],"When":{"..":1}}]}""", "ArrayKeys[0].When: Invalid key path")]
    [TestCase("""{"IgnoredKeys":[]}""", "unknown property \"IgnoredKeys\"")]
    [TestCase("""{"IgnoreKeys":"a"}""", "\"IgnoreKeys\" must be an array")]
    [TestCase("""{"IgnoreKeys":[1]}""", "IgnoreKeys[0] must be an object")]
    [TestCase("""{"IgnoreKeys":[{}]}""", "IgnoreKeys[0].Keys")]
    [TestCase("""{"IgnoreKeys":[{"Keys":[]}]}""", "IgnoreKeys[0].Keys")]
    [TestCase("""{"IgnoreKeys":[{"Keys":[1]}]}""", "IgnoreKeys[0].Keys")]
    [TestCase("""{"IgnoreKeys":["a",{"Key":["a"]}]}""", "IgnoreKeys[1] has an unknown property \"Key\"")]
    [TestCase("""{"IgnoreKeys":[{"Keys":["a"],"When":["T"]}]}""", "IgnoreKeys[0].When")]
    [TestCase("""{"IgnoreKeys":[{"Keys":["a"],"When":{"a[":1}}]}""", "IgnoreKeys[0].When: Invalid key path")]
    public void Parse_InvalidRules_ThrowWithLocation(string json, string message)
    {
        Assert.That(() => MatchRulesFileLoader.Parse(json), Throws.InstanceOf<JsonException>().With.Message.Contains(message));
    }

    [Test]
    public void Load_NoFile_IsEmpty()
    {
        var loader = new MatchRulesFileLoader([Path.Combine(_directory, "missing.json")]);

        Assert.That(loader.Load(), Is.SameAs(MatchRules.Empty));
    }

    [Test]
    public void Load_UsesTheFirstExistingCandidate()
    {
        var user = Path.Combine(_directory, "user.json");
        var shipped = WriteFile("shipped.json", """{"ArrayKeys":[{"Key":["Shipped"]}]}""");
        var loader = new MatchRulesFileLoader([user, shipped]);

        Assert.That(loader.Load().ArrayKeys.Single().Key.Single().ToString(), Is.EqualTo("Shipped"));

        WriteFile("user.json", """{"ArrayKeys":[{"Key":["User"]}]}""");
        Assert.That(loader.Load().ArrayKeys.Single().Key.Single().ToString(), Is.EqualTo("User"));
    }

    [Test]
    public void Load_InvalidFile_NamesTheFile()
    {
        var path = WriteFile("bad.json", "{ nope");

        Assert.That(() => new MatchRulesFileLoader([path]).Load(),
            Throws.InstanceOf<JsonException>().With.Message.StartsWith(path));
    }

    [Test]
    public void ShippedFile_IsAValidExampleWithoutRules()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, MatchRulesFileLoader.FileName);

        var rules = new MatchRulesFileLoader([path]).Load();

        Assert.Multiple(() =>
        {
            Assert.That(rules.ArrayKeys, Is.Empty);
            Assert.That(rules.IgnoreKeys, Is.Empty);
        });
    }
}
