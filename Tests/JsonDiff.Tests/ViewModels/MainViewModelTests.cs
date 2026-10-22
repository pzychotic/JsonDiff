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

    [SetUp]
    public void SetUp()
    {
        _dialogs = new FakeDialogService();
        _loader = new FakeJsonFileLoader();
        _rules = new FakeMatchRulesLoader();
        _loader.Files["a.json"] = """{"x":1,"y":2}""";
        _loader.Files["b.json"] = """{"x":1,"y":3}""";
        _loader.Files["c.json"] = """{"y":2,"x":1}""";
    }

    private MainViewModel Create() => new(_dialogs, _loader, _rules);

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
            Assert.That(vm.Left.Lines.Select(l => l.Kind), Is.All.EqualTo(DiffLineKind.Unchanged));
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
            Assert.That(vm.Right.Lines[1].Text.Trim(), Is.EqualTo("\"x\": 1,"));
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
            Assert.That(vm.Left.Lines.Single(l => l.Text.Contains("\"y\"")).Kind, Is.EqualTo(DiffLineKind.Ignored));
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
}
