using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class DiffHunksTests
{
    /// <summary>
    /// A diff from one character per row and side: '.' unchanged, 'u' unmatched, 'g' gap, 'm' modified,
    /// 't' type changed, 'i' ignored. Lines are numbered from 1 per side, skipping gaps.
    /// </summary>
    private static DiffResult Rows(string left, string right)
    {
        return new DiffResult(Pane(left), Pane(right), new DiffSummary(0, 0, 0, 0));

        static IReadOnlyList<DiffLine> Pane(string kinds)
        {
            var number = 0;
            return [.. kinds.Select((c, i) => new DiffLine(Kind(c), i.ToString(), c == 'g' ? null : ++number))];
        }

        static DiffLineKind Kind(char c) => c switch
        {
            '.' => DiffLineKind.Unchanged,
            'u' => DiffLineKind.Unmatched,
            'g' => DiffLineKind.Gap,
            'm' => DiffLineKind.Modified,
            't' => DiffLineKind.TypeChanged,
            'i' => DiffLineKind.Ignored,
            _ => throw new ArgumentOutOfRangeException(nameof(c)),
        };
    }

    [Test]
    public void Build_NoDifferences_ReturnsNoHunks()
    {
        Assert.That(DiffHunks.Build(Rows("..i.", "..i."), 3), Is.Empty);
    }

    [Test]
    public void Build_AddsContextAroundADifference()
    {
        var hunks = DiffHunks.Build(Rows("....m....", "....m...."), 2);

        Assert.That(hunks, Is.EqualTo(new[] { new DiffHunk(2, 6, 3, 5, 3, 5) }));
    }

    [Test]
    public void Build_ClipsContextAtTheEnds()
    {
        var hunks = DiffHunks.Build(Rows("m..m", "m..m"), 5);

        Assert.That(hunks, Is.EqualTo(new[] { new DiffHunk(0, 3, 1, 4, 1, 4) }));
    }

    // The blocks at rows 2 and 6 have 3 rows between them: context 1 leaves one row out, context 2 shares row 4.
    [TestCase(0, 2)]
    [TestCase(1, 2)]
    [TestCase(2, 1)]
    public void Build_MergesBlocksWhoseContextOverlaps(int context, int expectedHunks)
    {
        var hunks = DiffHunks.Build(Rows("..m...m..", "..m...m.."), context);

        Assert.That(hunks, Has.Count.EqualTo(expectedHunks));
    }

    [Test]
    public void Build_MergesBlocksWhoseContextIsAdjacent()
    {
        // Context 1 around rows 1 and 4 covers rows 0-2 and 3-5: no row between them, so one hunk.
        var hunks = DiffHunks.Build(Rows(".m..m.", ".m..m."), 1);

        Assert.That(hunks, Is.EqualTo(new[] { new DiffHunk(0, 5, 1, 6, 1, 6) }));
    }

    [Test]
    public void Build_SideWithOnlyGaps_StartsAtTheLineBefore()
    {
        // Rows 2-3 exist only on the right; with no context the left side has no lines in the hunk.
        var hunks = DiffHunks.Build(Rows("..gg..", "..uu.."), 0);

        Assert.That(hunks, Is.EqualTo(new[] { new DiffHunk(2, 3, 2, 0, 3, 2) }));
    }

    [Test]
    public void Build_InsertionAtTheStart_StartsAtZero()
    {
        var hunks = DiffHunks.Build(Rows("g..", "u.."), 0);

        Assert.That(hunks, Is.EqualTo(new[] { new DiffHunk(0, 0, 0, 0, 1, 1) }));
    }

    [Test]
    public void Build_NegativeContext_Throws()
    {
        Assert.That(() => DiffHunks.Build(Rows("m", "m"), -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
