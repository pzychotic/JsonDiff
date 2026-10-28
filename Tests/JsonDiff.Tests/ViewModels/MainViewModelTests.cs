using JsonDiff.Models;
using JsonDiff.Tests.TestSupport;
using JsonDiff.ViewModels;

namespace JsonDiff.Tests.ViewModels;

[TestFixture]
public class MainViewModelTests
{
    private FakeDialogService _dialogs = null!;
    private FakeJsonFileLoader _loader = null!;
    private FakeMatchRulesLoader _rules = null!;
    private FakeThemeService _themes = null!;

    [SetUp]
    public void SetUp()
    {
        _dialogs = new FakeDialogService();
        _loader = new FakeJsonFileLoader();
        _rules = new FakeMatchRulesLoader();
        _themes = new FakeThemeService();
        _loader.Files["a.json"] = """{"x":1,"y":2}""";
        _loader.Files["b.json"] = """{"x":1,"y":3}""";
        _loader.Files["c.json"] = """{"y":2,"x":1}""";
    }

    private MainViewModel Create() => new(_dialogs, _loader, _rules, _themes);

    private static DiffLine[] Lines(FilePaneViewModel pane) => [.. pane.Lines.Cast<DiffLine>()];

    /// <summary>An object of <paramref name="count"/> members m00…, with "different" values at the given members.</summary>
    private static string Members(int count, params int[] different) =>
        "{" + string.Join(",", Enumerable.Range(0, count)
            .Select(i => $"\"m{i:D2}\":{(different.Contains(i) ? 1 : 0)}")) + "}";

    /// <summary>Two 30-member files that differ at members 5 and 20, i.e. rows 6 and 21.</summary>
    private MainViewModel CreateWithTwoDifferences()
    {
        _loader.Files["long-a.json"] = Members(30);
        _loader.Files["long-b.json"] = Members(30, 5, 20);
        var vm = Create();
        vm.LoadFiles(["long-a.json", "long-b.json"]);
        return vm;
    }

    [Test]
    public void Constructor_StartsEmptyWithSortedKeys()
    {
        var vm = Create();

        Assert.Multiple(() =>
        {
            Assert.That(vm.SortKeys, Is.True);
            Assert.That(vm.Left.Lines, Is.Empty);
            Assert.That(vm.Right.Lines, Is.Empty);
            Assert.That(vm.Summary, Is.Null);
            Assert.That(vm.ReloadCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void LoadFiles_BothSides_ComputesTheDiff()
    {
        var vm = Create();

        vm.LoadFiles(["a.json", "b.json"]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Summary, Is.EqualTo(new DiffSummary(1, 0, 0, 0)));
            Assert.That(vm.Left.Lines, Has.Count.EqualTo(vm.Right.Lines.Count));
            Assert.That(vm.StatusText, Is.EqualTo("1 difference: 1 modified."));
        });
    }

    [Test]
    public void LoadFiles_OneSide_ShowsItWithoutDiff()
    {
        var vm = Create();

        vm.LoadFiles(["a.json"]);

        Assert.Multiple(() =>
        {
            Assert.That(Lines(vm.Left).Select(l => l.Kind), Is.All.EqualTo(DiffLineKind.Unchanged));
            Assert.That(vm.Right.Lines, Is.Empty);
            Assert.That(vm.Summary, Is.Null);
        });
    }

    [Test]
    public void LoadFiles_UnreadableFile_ShowsErrorAndKeepsPaneEmpty()
    {
        var vm = Create();

        vm.LoadFiles(["missing.json"]);

        Assert.Multiple(() =>
        {
            Assert.That(_dialogs.Errors, Has.Count.EqualTo(1));
            Assert.That(_dialogs.Errors[0], Does.Contain("missing.json"));
            Assert.That(vm.Left.HasDocument, Is.False);
        });
    }

    [Test]
    public void LoadFiles_InvalidJson_ShowsErrorAndKeepsPreviousDocument()
    {
        _loader.Files["bad.json"] = "{";
        var vm = Create();
        vm.LoadFiles(["a.json"]);

        vm.LoadFiles(["bad.json"]);

        Assert.Multiple(() =>
        {
            Assert.That(_dialogs.Errors, Has.Count.EqualTo(1));
            Assert.That(vm.Left.FilePath, Is.EqualTo("a.json"));
        });
    }

    [Test]
    public void CompareNumbers_Toggle_RecomputesTheDiff()
    {
        _loader.Files["n1.json"] = """{"x":"5.40812"}""";
        _loader.Files["n2.json"] = """{"x":"5.408"}""";
        var vm = Create();
        vm.LoadFiles(["n1.json", "n2.json"]);
        Assume.That(vm.Summary!.IsIdentical, Is.False);

        vm.CompareNumbers = true;

        Assert.Multiple(() =>
        {
            Assert.That(vm.Summary!.IsIdentical, Is.True);
            Assert.That(vm.StatusText, Is.EqualTo("No differences."));
        });
    }

    [TestCase("0.01")]
    [TestCase("0,01")]
    [TestCase(" 1e-2 ")]
    public void ToleranceText_Valid_RecomputesTheDiff(string text)
    {
        _loader.Files["n1.json"] = """{"x":"1.01"}""";
        _loader.Files["n2.json"] = """{"x":"1"}""";
        var vm = Create();
        vm.CompareNumbers = true;
        vm.LoadFiles(["n1.json", "n2.json"]);
        Assume.That(vm.Summary!.IsIdentical, Is.False);

        vm.ToleranceText = text;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsToleranceValid, Is.True);
            Assert.That(vm.Summary!.IsIdentical, Is.True);
        });
    }

    [TestCase("abc")]
    [TestCase("-1")]
    [TestCase("")]
    public void ToleranceText_Invalid_KeepsThePreviousTolerance(string text)
    {
        _loader.Files["n1.json"] = """{"x":"1.0004"}""";
        _loader.Files["n2.json"] = """{"x":"1"}""";
        var vm = Create();
        vm.CompareNumbers = true;
        vm.LoadFiles(["n1.json", "n2.json"]);
        Assume.That(vm.Summary!.IsIdentical, Is.True);

        vm.ToleranceText = text;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsToleranceValid, Is.False);
            Assert.That(vm.Summary!.IsIdentical, Is.True);
        });
    }

    [Test]
    public void ToleranceText_WhileCompareNumbersOff_AppliesWhenTurnedOn()
    {
        _loader.Files["n1.json"] = """{"x":"1.01"}""";
        _loader.Files["n2.json"] = """{"x":"1"}""";
        var vm = Create();
        vm.LoadFiles(["n1.json", "n2.json"]);

        vm.ToleranceText = "0.1";
        var whileOff = vm.Summary;
        vm.CompareNumbers = true;

        Assert.Multiple(() =>
        {
            Assert.That(whileOff!.IsIdentical, Is.False);
            Assert.That(vm.Summary!.IsIdentical, Is.True);
        });
    }

    [Test]
    public void SortKeys_Toggle_RecomputesTheDiff()
    {
        var vm = Create();
        vm.LoadFiles(["a.json", "c.json"]);
        Assume.That(vm.Summary!.IsIdentical, Is.True);

        vm.SortKeys = false;

        // Key order never counts as a difference, but the right pane now follows the left's order.
        Assert.Multiple(() =>
        {
            Assert.That(vm.Summary!.IsIdentical, Is.True);
            Assert.That(Lines(vm.Right)[1].Text.Trim(), Is.EqualTo("\"x\": 1,"));
        });
    }

    [Test]
    public void OpenLeft_LoadsTheChosenFile()
    {
        _dialogs.OpenResult = "a.json";
        var vm = Create();

        vm.OpenLeftCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_dialogs.OpenTitles, Is.EqualTo(new[] { "Open Left File" }));
            Assert.That(vm.Left.FilePath, Is.EqualTo("a.json"));
            Assert.That(vm.Left.Lines, Is.Not.Empty);
        });
    }

    [Test]
    public void OpenRight_Canceled_ChangesNothing()
    {
        var vm = Create();

        vm.OpenRightCommand.Execute(null);

        Assert.That(vm.Right.HasDocument, Is.False);
    }

    [Test]
    public void DropRight_SingleFile_LoadsIntoThatPane()
    {
        var vm = Create();

        vm.DropRightCommand.Execute(new[] { "b.json" });

        Assert.Multiple(() =>
        {
            Assert.That(vm.Right.FilePath, Is.EqualTo("b.json"));
            Assert.That(vm.Left.HasDocument, Is.False);
        });
    }

    [Test]
    public void DropRight_TwoFiles_FillsBothPanesInOrder()
    {
        var vm = Create();

        vm.DropRightCommand.Execute(new[] { "a.json", "b.json" });

        Assert.Multiple(() =>
        {
            Assert.That(vm.Left.FilePath, Is.EqualTo("a.json"));
            Assert.That(vm.Right.FilePath, Is.EqualTo("b.json"));
        });
    }

    [Test]
    public void Swap_ExchangesSides()
    {
        var vm = Create();
        vm.LoadFiles(["a.json"]);

        vm.SwapCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Left.HasDocument, Is.False);
            Assert.That(vm.Right.FilePath, Is.EqualTo("a.json"));
            Assert.That(vm.Right.Lines, Is.Not.Empty);
        });
    }

    [Test]
    public void Reload_PicksUpChangedFiles()
    {
        var vm = Create();
        vm.LoadFiles(["a.json", "b.json"]);

        _loader.Files["b.json"] = _loader.Files["a.json"];
        vm.ReloadCommand.Execute(null);

        Assert.That(vm.Summary!.IsIdentical, Is.True);
    }

    // --- Match rules --------------------------------------------------------

    private static readonly MatchRules ByName = new([new([JsonKeyPath.Parse("Name")], [])], []);

    private void LoadSwappedArrays(MainViewModel vm)
    {
        _loader.Files["l.json"] = """[{"Name":"a"},{"Name":"b"}]""";
        _loader.Files["r.json"] = """[{"Name":"b"},{"Name":"a"}]""";
        vm.LoadFiles(["l.json", "r.json"]);
    }

    [Test]
    public void LoadMatchRules_AppliesThemToTheDiff()
    {
        var vm = Create();
        LoadSwappedArrays(vm);
        Assert.That(vm.Summary!.IsIdentical, Is.False);

        _rules.Rules = ByName;
        vm.LoadMatchRules();

        Assert.That(vm.Summary!.IsIdentical, Is.True);
    }

    [Test]
    public void LoadMatchRules_AppliesIgnoreRulesToTheDiff()
    {
        var vm = Create();
        vm.LoadFiles(["a.json", "b.json"]);
        Assert.That(vm.Summary!.IsIdentical, Is.False);

        _rules.Rules = new MatchRules([], [new IgnoreRule(["y"], [])]);
        vm.LoadMatchRules();

        Assert.Multiple(() =>
        {
            Assert.That(vm.Summary!.IsIdentical, Is.True);
            Assert.That(Lines(vm.Left).Single(l => l.Text.Contains("\"y\"")).Kind, Is.EqualTo(DiffLineKind.Ignored));
        });
    }

    [Test]
    public void Reload_PicksUpChangedMatchRules()
    {
        var vm = Create();
        LoadSwappedArrays(vm);

        _rules.Rules = ByName;
        vm.ReloadCommand.Execute(null);

        Assert.That(vm.Summary!.IsIdentical, Is.True);
    }

    [Test]
    public void LoadMatchRules_InvalidRules_ShowErrorAndKeepThePreviousRules()
    {
        var vm = Create();
        _rules.Rules = ByName;
        vm.LoadMatchRules();
        LoadSwappedArrays(vm);

        _rules.Error = "broken";
        vm.LoadMatchRules();

        Assert.Multiple(() =>
        {
            Assert.That(_dialogs.Errors, Has.One.Contains("broken"));
            Assert.That(vm.Summary!.IsIdentical, Is.True);
        });
    }

    // --- Changes only and navigation ----------------------------------------

    [Test]
    public void ShowOverview_IsOnByDefault()
    {
        Assert.That(Create().ShowOverview, Is.True);
    }

    [Test]
    public void ShowChangesOnly_FoldsTheRowsAwayFromTheDifferences()
    {
        var vm = CreateWithTwoDifferences();
        Assert.That(vm.Left.Lines, Has.Count.EqualTo(32));

        vm.ShowChangesOnly = true;

        Assert.Multiple(() =>
        {
            // [0+4], rows 4-8, [9+10], rows 19-23, [24+8]
            Assert.That(vm.Left.Lines, Has.Count.EqualTo(13));
            Assert.That(vm.Right.Lines, Has.Count.EqualTo(13));
            Assert.That(vm.Left.Lines[0], Is.EqualTo(new FoldLine(0, 4)));
            Assert.That(vm.Right.Lines[6], Is.EqualTo(new FoldLine(9, 10)));
        });
    }

    [Test]
    public void ExpandFold_ShowsTheHiddenRows()
    {
        var vm = CreateWithTwoDifferences();
        vm.ShowChangesOnly = true;

        vm.ExpandFoldCommand.Execute(vm.Left.Lines[0]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Left.Lines, Has.Count.EqualTo(16));
            Assert.That(vm.Left.Lines.Take(4), Is.All.InstanceOf<DiffLine>());
        });
    }

    [Test]
    public void ContextLinesText_Valid_RefoldsWithThatContext()
    {
        var vm = CreateWithTwoDifferences();
        vm.ShowChangesOnly = true;

        vm.ContextLinesText = "0";

        Assert.Multiple(() =>
        {
            Assert.That(vm.ContextLines, Is.Zero);
            Assert.That(vm.Left.Lines, Has.Count.EqualTo(5));
        });
    }

    [TestCase("x")]
    [TestCase("-1")]
    [TestCase("100")]
    public void ContextLinesText_Invalid_KeepsThePreviousContext(string text)
    {
        var vm = CreateWithTwoDifferences();
        vm.ShowChangesOnly = true;

        vm.ContextLinesText = text;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsContextLinesValid, Is.False);
            Assert.That(vm.ContextLines, Is.EqualTo(2));
            Assert.That(vm.Left.Lines, Has.Count.EqualTo(13));
        });
    }

    [Test]
    public void NextDifference_ScrollsEachDifferenceBelowTheContextRows()
    {
        var vm = CreateWithTwoDifferences();

        vm.NextDifferenceCommand.Execute(null);
        var first = vm.Left.TopRow;
        vm.NextDifferenceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(4));
            Assert.That(vm.Left.TopRow, Is.EqualTo(19));
            Assert.That(vm.Right.TopRow, Is.EqualTo(19));
            Assert.That(vm.NextDifferenceCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void PreviousDifference_GoesBackToTheEarlierDifference()
    {
        var vm = CreateWithTwoDifferences();
        Assert.That(vm.PreviousDifferenceCommand.CanExecute(null), Is.False);
        vm.Left.TopRow = 25;

        vm.PreviousDifferenceCommand.Execute(null);
        var first = vm.Left.TopRow;
        vm.PreviousDifferenceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(19));
            Assert.That(vm.Left.TopRow, Is.EqualTo(4));
            Assert.That(vm.PreviousDifferenceCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void NextDifference_InChangesOnly_UsesTheShownRows()
    {
        var vm = CreateWithTwoDifferences();
        vm.ShowChangesOnly = true;

        vm.NextDifferenceCommand.Execute(null);
        var first = vm.Left.TopRow;
        vm.NextDifferenceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(1));
            Assert.That(vm.Left.TopRow, Is.EqualTo(7));
        });
    }

    [Test]
    public void NextDifference_AtTheEndOfTheView_IsDisabled()
    {
        var vm = CreateWithTwoDifferences();
        vm.Left.ViewportRows = 20;

        vm.Left.TopRow = 12;

        Assert.That(vm.NextDifferenceCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void ShowChangesOnly_Toggle_KeepsTheTopRowInView()
    {
        var vm = CreateWithTwoDifferences();
        vm.Left.TopRow = 19;

        vm.ShowChangesOnly = true;
        var folded = vm.Left.TopRow;
        vm.ShowChangesOnly = false;

        Assert.Multiple(() =>
        {
            Assert.That(folded, Is.EqualTo(7));
            Assert.That(vm.Left.TopRow, Is.EqualTo(19));
        });
    }

    [Test]
    public void NavigationCommands_OneSideOnly_AreDisabled()
    {
        var vm = Create();

        vm.LoadFiles(["a.json"]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.NextDifferenceCommand.CanExecute(null), Is.False);
            Assert.That(vm.PreviousDifferenceCommand.CanExecute(null), Is.False);
        });
    }

    // --- Search ---------------------------------------------------------------

    /// <summary>Opens the search over the two-difference files, in a view of 10 rows.</summary>
    private MainViewModel CreateSearching()
    {
        var vm = CreateWithTwoDifferences();
        vm.Left.ViewportRows = 10;
        vm.IsSearchOpen = true;
        return vm;
    }

    private static IReadOnlyList<TextHighlight> HighlightsAt(FilePaneViewModel pane, int row) =>
        pane.SearchHighlights!.For((DiffLine)pane.Lines[row]);

    [Test]
    public void SearchText_HighlightsTheHitsAndGoesToTheFirst()
    {
        var vm = CreateSearching();

        vm.SearchText = "\"M2";

        Assert.Multiple(() =>
        {
            Assert.That(vm.SearchStatus, Is.EqualTo("1 of 10"));
            Assert.That(vm.HasNoMatches, Is.False);
            Assert.That(vm.Left.SearchRows, Is.EqualTo(Enumerable.Range(21, 10)));
            Assert.That(HighlightsAt(vm.Left, 21), Is.EqualTo(new[] { new TextHighlight(2, 3, IsCurrent: true) }));
            Assert.That(HighlightsAt(vm.Left, 22), Is.EqualTo(new[] { new TextHighlight(2, 3, IsCurrent: false) }));
            Assert.That(vm.Left.TopRow, Is.EqualTo(16));
            Assert.That(vm.Right.SearchHighlights, Is.Null);
        });
    }

    [Test]
    public void SearchText_StartsAtTheTopRowAndKeepsAHitInView()
    {
        var vm = CreateSearching();
        vm.Left.TopRow = 15;

        vm.SearchText = "\"m";

        Assert.Multiple(() =>
        {
            Assert.That(vm.SearchStatus, Is.EqualTo("15 of 30"));
            Assert.That(vm.Left.TopRow, Is.EqualTo(15));
        });
    }

    [Test]
    public void SearchText_NoHit_SaysSo()
    {
        var vm = CreateSearching();

        vm.SearchText = "nothing";

        Assert.Multiple(() =>
        {
            Assert.That(vm.SearchStatus, Is.EqualTo("No matches"));
            Assert.That(vm.HasNoMatches, Is.True);
            Assert.That(vm.Left.SearchRows, Is.Empty);
        });
    }

    [Test]
    public void SearchSide_SearchesTheOtherPane()
    {
        var vm = CreateSearching();
        vm.SearchText = ": 1";
        var onLeft = vm.SearchStatus;

        vm.SearchSide = PaneSide.Right;

        Assert.Multiple(() =>
        {
            Assert.That(onLeft, Is.EqualTo("No matches"));
            Assert.That(vm.SearchStatus, Is.EqualTo("1 of 2"));
            Assert.That(vm.Right.SearchRows, Is.EqualTo(new[] { 6, 21 }));
            Assert.That(vm.Left.SearchHighlights, Is.Null);
        });
    }

    [Test]
    public void FindNextAndPrevious_WrapAround()
    {
        var vm = CreateSearching();
        vm.SearchSide = PaneSide.Right;
        vm.SearchText = ": 1";

        var statuses = new List<string>();
        foreach (var command in new[] { vm.FindNextCommand, vm.FindNextCommand, vm.FindPreviousCommand })
        {
            command.Execute(null);
            statuses.Add(vm.SearchStatus);
        }

        Assert.Multiple(() =>
        {
            Assert.That(statuses, Is.EqualTo(new[] { "2 of 2", "1 of 2", "2 of 2" }));
            Assert.That(vm.Left.TopRow, Is.EqualTo(16));
        });
    }

    [Test]
    public void SearchMatchCaseAndWholeWord_NarrowTheHits()
    {
        var vm = CreateSearching();
        vm.SearchText = "M0";
        var anyCase = vm.SearchStatus;

        vm.SearchMatchCase = true;
        var matchCase = vm.SearchStatus;
        vm.SearchMatchCase = false;
        vm.SearchWholeWord = true;

        Assert.Multiple(() =>
        {
            Assert.That(anyCase, Is.EqualTo("1 of 10"));
            Assert.That(matchCase, Is.EqualTo("No matches"));
            Assert.That(vm.SearchStatus, Is.EqualTo("No matches"));
        });
    }

    [Test]
    public void FindNext_HitInAFold_ExpandsItAndScrollsToIt()
    {
        var vm = CreateSearching();
        vm.ShowChangesOnly = true;

        vm.SearchText = "m12";

        var row = vm.Left.SearchRows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(((DiffLine)vm.Left.Lines[row]).Text, Does.Contain("m12"));
            Assert.That(HighlightsAt(vm.Left, row).Single().IsCurrent, Is.True);
            Assert.That(row, Is.InRange(vm.Left.TopRow, vm.Left.TopRow + vm.Left.ViewportRows - 1));
        });
    }

    [Test]
    public void SortKeys_Toggle_KeepsTheSearch()
    {
        var vm = CreateSearching();
        vm.SearchText = "m05";

        vm.SortKeys = false;

        Assert.Multiple(() =>
        {
            Assert.That(vm.SearchStatus, Is.EqualTo("1 of 1"));
            Assert.That(HighlightsAt(vm.Left, 6), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void CloseSearch_ClearsTheHitsButKeepsTheText()
    {
        var vm = CreateSearching();
        vm.SearchText = "m05";

        vm.CloseSearchCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsSearchOpen, Is.False);
            Assert.That(vm.SearchText, Is.EqualTo("m05"));
            Assert.That(vm.SearchStatus, Is.Empty);
            Assert.That(vm.Left.SearchHighlights, Is.Null);
            Assert.That(vm.Left.SearchRows, Is.Empty);
            Assert.That(vm.CloseSearchCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void FindNext_WhileClosed_ReopensTheSearch()
    {
        var vm = CreateSearching();
        vm.SearchText = "m05";
        vm.CloseSearchCommand.Execute(null);

        vm.FindNextCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsSearchOpen, Is.True);
            Assert.That(vm.SearchStatus, Is.EqualTo("1 of 1"));
        });
    }

    [Test]
    public void FindNext_WithoutText_IsDisabled()
    {
        Assert.That(CreateSearching().FindNextCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Search_OneSideOnly_SearchesItsLines()
    {
        var vm = Create();
        vm.LoadFiles(["a.json"]);
        vm.IsSearchOpen = true;

        vm.SearchText = "\"y\"";

        Assert.Multiple(() =>
        {
            Assert.That(vm.SearchStatus, Is.EqualTo("1 of 1"));
            Assert.That(vm.Left.SearchRows, Is.EqualTo(new[] { 2 }));
        });
    }

    [Test]
    public void SetTheme_AppliesTheChosenTheme()
    {
        var vm = Create();

        vm.SetThemeCommand.Execute(AppTheme.Dark);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Theme, Is.EqualTo(AppTheme.Dark));
            Assert.That(_themes.Applied, Is.EqualTo(new[] { AppTheme.Dark }));
        });
    }
}
