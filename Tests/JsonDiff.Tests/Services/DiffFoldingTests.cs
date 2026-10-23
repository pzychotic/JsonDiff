using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class DiffFoldingTests
{
    private static readonly IReadOnlySet<int> NoneExpanded = new HashSet<int>();

    /// <summary>
    /// A diff from one character per row and side: '.' unchanged, 'u' unmatched, 'g' gap, 'm' modified,
    /// 't' type changed, 'i' ignored. Each row's text is its index.
    /// </summary>
    private static DiffResult Rows(string left, string right)
    {
        return new DiffResult(Pane(left), Pane(right), new DiffSummary(0, 0, 0, 0));

        static IReadOnlyList<DiffLine> Pane(string kinds) =>
            [.. kinds.Select((c, i) => new DiffLine(Kind(c), i.ToString(), c == 'g' ? null : i + 1))];

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

    /// <summary>The shown rows as text: a row's index, or "[start+count]" for a fold.</summary>
    private static string[] Shown(IEnumerable<object> rows) =>
        [.. rows.Select(r => r switch
        {
            DiffLine line => line.Text,
            FoldLine fold => $"[{fold.Start}+{fold.Count}]",
            _ => throw new InvalidOperationException(),
        })];

    // --- FindBlocks ---------------------------------------------------------

    [Test]
    public void FindBlocks_GroupsAdjacentDifferences()
    {
        var blocks = DiffFolding.FindBlocks(Rows(".mm..u.t", ".mm..g.t"));

        Assert.That(blocks, Is.EqualTo(new[] { new DiffBlock(1, 2), new DiffBlock(5, 5), new DiffBlock(7, 7) }));
    }

    [Test]
    public void FindBlocks_CountsADifferenceOnEitherSide()
    {
        var blocks = DiffFolding.FindBlocks(Rows(".g.", ".u."));

        Assert.That(blocks, Is.EqualTo(new[] { new DiffBlock(1, 1) }));
    }

    [Test]
    public void FindBlocks_IgnoredRowsAreNoDifference()
    {
        var blocks = DiffFolding.FindBlocks(Rows(".ii.", ".ig."));

        Assert.That(blocks, Is.Empty);
    }

    // --- Fold ---------------------------------------------------------------

    [Test]
    public void Fold_KeepsContextAroundEachBlock()
    {
        var result = Rows("......m......", "......m......");

        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 2, NoneExpanded);

        Assert.Multiple(() =>
        {
            Assert.That(Shown(view.Left), Is.EqualTo(new[] { "[0+4]", "4", "5", "6", "7", "8", "[9+4]" }));
            Assert.That(Shown(view.Right), Is.EqualTo(Shown(view.Left)));
            Assert.That(view.FullRows, Is.EqualTo(new[] { 0, 4, 5, 6, 7, 8, 9 }));
        });
    }

    [Test]
    public void Fold_MergesBlocksWhoseContextsTouch()
    {
        var result = Rows("m....m", "m....m");

        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 2, NoneExpanded);

        Assert.That(Shown(view.Left), Is.EqualTo(new[] { "0", "1", "2", "3", "4", "5" }));
    }

    [Test]
    public void Fold_ShowsASingleHiddenRowInsteadOfFoldingIt()
    {
        var result = Rows("m.....m", "m.....m");

        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 2, NoneExpanded);

        Assert.That(Shown(view.Left), Is.EqualTo(new[] { "0", "1", "2", "3", "4", "5", "6" }));
    }

    [Test]
    public void Fold_ShowsExpandedRuns()
    {
        var result = Rows("m......", "m......");

        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 1, new HashSet<int> { 2 });

        Assert.That(Shown(view.Left), Is.EqualTo(new[] { "0", "1", "2", "3", "4", "5", "6" }));
    }

    [Test]
    public void Fold_WithoutContext_ShowsOnlyTheDifferences()
    {
        var result = Rows("...m...", "...g...");

        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 0, NoneExpanded);

        Assert.That(Shown(view.Right), Is.EqualTo(new[] { "[0+3]", "3", "[4+3]" }));
    }

    [Test]
    public void Fold_IdenticalRows_BecomeASingleFold()
    {
        var result = Rows(".i..", ".g..");

        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 2, NoneExpanded);

        Assert.That(Shown(view.Left), Is.EqualTo(new[] { "[0+4]" }));
    }

    // --- Row mapping --------------------------------------------------------

    [Test]
    public void ToShownRow_MapsHiddenRowsToTheNextShownRow()
    {
        var result = Rows("......m", "......m");
        var view = DiffFolding.Fold(result, DiffFolding.FindBlocks(result), 1, NoneExpanded);

        Assert.Multiple(() =>
        {
            Assert.That(view.ToShownRow(0), Is.EqualTo(0));
            Assert.That(view.ToShownRow(3), Is.EqualTo(1));
            Assert.That(view.ToShownRow(6), Is.EqualTo(2));
            Assert.That(view.ToContainingRow(3), Is.EqualTo(0));
            Assert.That(view.ToContainingRow(5), Is.EqualTo(1));
        });
    }
}
