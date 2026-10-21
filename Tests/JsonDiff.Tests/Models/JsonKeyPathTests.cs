using System.Text.Json.Nodes;
using JsonDiff.Models;

namespace JsonDiff.Tests.Models;

[TestFixture]
public class JsonKeyPathTests
{
    private const string Item = """
        {"Type":"T","Fields":[
          {"Name":"Id","Value":{"Other":"o"}},
          {"Name":"Size","Value":{"X":1}},
          {"Name":"Id","Value":{"Label":"l1"}}]}
        """;

    private static JsonNode? Resolve(string path, string json = Item) =>
        JsonKeyPath.Parse(path).TryResolve(JsonNode.Parse(json), out var value) ? value : throw new KeyNotFoundException(path);

    private static bool Resolves(string path, string json = Item) =>
        JsonKeyPath.Parse(path).TryResolve(JsonNode.Parse(json), out _);

    [Test]
    public void TryResolve_TopLevelProperty()
    {
        Assert.That(Resolve("Type")!.GetValue<string>(), Is.EqualTo("T"));
    }

    [Test]
    public void TryResolve_FilteredArrayElement()
    {
        Assert.That(Resolve("Fields[Name=Size].Value.X")!.GetValue<int>(), Is.EqualTo(1));
    }

    [Test]
    public void TryResolve_SeveralElementsPassTheFilter_TakesTheFirstThatHasTheRestOfThePath()
    {
        Assert.That(Resolve("Fields[Name=Id].Value.Label")!.GetValue<string>(), Is.EqualTo("l1"));
    }

    [Test]
    public void TryResolve_NullValue_Resolves()
    {
        Assert.That(Resolve("a", """{"a":null}"""), Is.Null);
    }

    [TestCase("Missing")]
    [TestCase("Type.Deeper")]
    [TestCase("Fields[Name=Nope].Value")]
    [TestCase("Type[Name=Id]")]
    [TestCase("Fields[Name=Id].Value.Missing")]
    public void TryResolve_MissingStep_IsFalse(string path)
    {
        Assert.That(Resolves(path), Is.False);
    }

    [Test]
    public void TryResolve_OnNonObject_IsFalse()
    {
        Assert.That(Resolves("a", "[1]"), Is.False);
    }

    [Test]
    public void Parse_FilterValueMayContainDots()
    {
        Assert.That(Resolve("a[k=x.y].v", """{"a":[{"k":"x.y","v":2}]}""")!.GetValue<int>(), Is.EqualTo(2));
    }

    [TestCase("")]
    [TestCase("a.")]
    [TestCase(".a")]
    [TestCase("a..b")]
    [TestCase("a[k=v")]
    [TestCase("a[kv]")]
    [TestCase("a[=v]")]
    [TestCase("a[k=v]b")]
    [TestCase("[k=v]")]
    public void Parse_InvalidPath_Throws(string path)
    {
        Assert.That(() => JsonKeyPath.Parse(path), Throws.TypeOf<FormatException>().With.Message.Contains(path));
    }

    [Test]
    public void ToString_IsTheParsedText()
    {
        Assert.That(JsonKeyPath.Parse("a[k=v].b").ToString(), Is.EqualTo("a[k=v].b"));
    }
}
