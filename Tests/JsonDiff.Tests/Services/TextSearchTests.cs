using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class TextSearchTests
{
    private static IReadOnlyList<DiffLine> Rows(params string[] texts) =>
        [.. texts.Select((text, i) => new DiffLine(DiffLineKind.Unchanged, text, i + 1))];

    [Test]
    public void Find_IgnoresCaseByDefault()
    {
        var matches = TextSearch.Find(Rows("\"Name\": 1", "\"name\": 2"), new SearchQuery("NAME"));

        Assert.That(matches, Is.EqualTo(new[] { new SearchMatch(0, 1, 4), new SearchMatch(1, 1, 4) }));
    }

    [Test]
    public void Find_MatchCase_SkipsOtherCases()
    {
        var matches = TextSearch.Find(Rows("\"Name\": 1", "\"name\": 2"), new SearchQuery("name", MatchCase: true));

        Assert.That(matches, Is.EqualTo(new[] { new SearchMatch(1, 1, 4) }));
    }

    [Test]
    public void Find_FindsSeveralHitsPerRowInOrder()
    {
        var matches = TextSearch.Find(Rows("aXaXa"), new SearchQuery("a"));

        Assert.That(matches.Select(m => m.Start), Is.EqualTo(new[] { 0, 2, 4 }));
    }

    [Test]
    public void Find_HitsDoNotOverlap()
    {
        var matches = TextSearch.Find(Rows("aaaa"), new SearchQuery("aa"));

        Assert.That(matches.Select(m => m.Start), Is.EqualTo(new[] { 0, 2 }));
    }

    [Test]
    public void Find_WholeWord_SkipsHitsInsideWords()
    {
        var matches = TextSearch.Find(Rows("\"id\": 1,", "\"userId\": 2,", "\"id_x\": 3"),
            new SearchQuery("id", WholeWord: true));

        Assert.That(matches, Is.EqualTo(new[] { new SearchMatch(0, 1, 2) }));
    }

    [Test]
    public void Find_WholeWord_AllowsPunctuationAtTheEdges()
    {
        var matches = TextSearch.Find(Rows("\"id\": 1,", "\"ids\": 2,"), new SearchQuery("\"id", WholeWord: true));

        Assert.That(matches, Is.EqualTo(new[] { new SearchMatch(0, 0, 3) }));
    }

    [Test]
    public void Find_TreatsTheTextLiterally()
    {
        var matches = TextSearch.Find(Rows("[1.5]", "[105]"), new SearchQuery("1.5"));

        Assert.That(matches, Is.EqualTo(new[] { new SearchMatch(0, 1, 3) }));
    }

    [Test]
    public void Find_SkipsGaps()
    {
        var rows = new[] { new DiffLine(DiffLineKind.Gap, "", null), new DiffLine(DiffLineKind.Unchanged, "x", 1) };

        Assert.That(TextSearch.Find(rows, new SearchQuery("x")), Is.EqualTo(new[] { new SearchMatch(1, 0, 1) }));
    }

    [Test]
    public void Find_EmptyText_FindsNothing()
    {
        Assert.That(TextSearch.Find(Rows("abc"), new SearchQuery("")), Is.Empty);
    }
}
