using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class OverviewMarksTests
{
    /// <summary>
    /// One pane's rows from one character per row: '.' unchanged, 'u' unmatched, 'g' gap, 'm' modified,
    /// 't' type changed, 'i' ignored, 'f' a fold.
    /// </summary>
    private static IReadOnlyList<object> Rows(string kinds) =>
        [.. kinds.Select((c, i) => c == 'f' ? (object)new FoldLine(i, 10) : new DiffLine(Kind(c), "", i + 1))];

    private static DiffLineKind Kind(char c) => c switch
    {
        '.' => DiffLineKind.Unchanged,
        'u' => DiffLineKind.Unmatched,
        'g' => DiffLineKind.Gap,
        'm' => DiffLineKind.Modified,
        't' => DiffLineKind.TypeChanged,
        'i' => DiffLineKind.Ignored,
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };

    [Test]
    public void Build_MapsRunsProportionally()
    {
        var marks = OverviewMarks.Build(Rows(".mm..u.."), 80);

        Assert.That(marks, Is.EqualTo(new[]
        {
            new OverviewMark(DiffLineKind.Unmatched, 50, 10),
            new OverviewMark(DiffLineKind.Modified, 10, 20),
        }));
    }

    [Test]
    public void Build_SplitsRunsAtAChangeOfKind()
    {
        var marks = OverviewMarks.Build(Rows("mt"), 20);

        Assert.That(marks, Is.EqualTo(new[]
        {
            new OverviewMark(DiffLineKind.Modified, 0, 10),
            new OverviewMark(DiffLineKind.TypeChanged, 10, 10),
        }));
    }

    [Test]
    public void Build_SkipsGapsUnchangedRowsAndFolds()
    {
        var marks = OverviewMarks.Build(Rows("mgmfm."), 60);

        Assert.That(marks.Select(m => m.Top), Is.EqualTo(new[] { 0.0, 20, 40 }));
    }

    [Test]
    public void Build_GivesASingleRowInABigFileTheMinimumHeight()
    {
        var rows = Rows(new string('.', 10_000) + "m" + new string('.', 9_999));

        var marks = OverviewMarks.Build(rows, 100, minHeight: 2);

        Assert.That(marks, Is.EqualTo(new[] { new OverviewMark(DiffLineKind.Modified, 50, 2) }));
    }

    [Test]
    public void Build_KeepsMarksInsideTheBar()
    {
        var marks = OverviewMarks.Build(Rows(new string('.', 999) + "u"), 100, minHeight: 2);

        Assert.That(marks, Is.EqualTo(new[] { new OverviewMark(DiffLineKind.Unmatched, 98, 2) }));
    }

    [Test]
    public void Build_OrdersTheStrongestKindLast()
    {
        var marks = OverviewMarks.Build(Rows("tmui"), 4);

        Assert.That(marks.Select(m => m.Kind), Is.EqualTo(new[]
        {
            DiffLineKind.Ignored, DiffLineKind.Unmatched, DiffLineKind.Modified, DiffLineKind.TypeChanged,
        }));
    }

    [Test]
    public void Build_WithoutRowsOrHeight_HasNoMarks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OverviewMarks.Build(Rows(""), 100), Is.Empty);
            Assert.That(OverviewMarks.Build(Rows("m"), 0), Is.Empty);
        });
    }
}
