using System.IO;
using System.Text.Json;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class JsonFileLoaderTests
{
    private string _path = "";

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"JsonDiffTests_{Guid.NewGuid():N}.json");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void Load_AcceptsCommentsAndTrailingCommas()
    {
        File.WriteAllText(_path, "{ // comment\n \"a\": [1, 2,], }");

        var node = new JsonFileLoader().Load(_path);

        Assert.That(node!["a"]!.AsArray(), Has.Count.EqualTo(2));
    }

    [Test]
    public void Load_InvalidJson_Throws()
    {
        File.WriteAllText(_path, "{ \"a\": ");

        Assert.That(() => new JsonFileLoader().Load(_path), Throws.InstanceOf<JsonException>());
    }

    [Test]
    public void Load_DuplicateKeys_Throws()
    {
        File.WriteAllText(_path, "{ \"a\": 1, \"a\": 2 }");

        Assert.That(() => new JsonFileLoader().Load(_path), Throws.InstanceOf<JsonException>());
    }
}
