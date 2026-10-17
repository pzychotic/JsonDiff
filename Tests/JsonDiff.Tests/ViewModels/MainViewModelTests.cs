using JsonDiff.Models;
using JsonDiff.Tests.TestSupport;
using JsonDiff.ViewModels;

namespace JsonDiff.Tests.ViewModels;

[TestFixture]
public class MainViewModelTests
{
    private FakeDialogService _dialogs = null!;
    private FakeJsonFileLoader _loader = null!;

    [SetUp]
    public void SetUp()
    {
        _dialogs = new FakeDialogService();
        _loader = new FakeJsonFileLoader();
        _loader.Files["a.json"] = """{"x":1,"y":2}""";
        _loader.Files["b.json"] = """{"x":1,"y":3}""";
        _loader.Files["c.json"] = """{"y":2,"x":1}""";
    }

    private MainViewModel Create() => new(_dialogs, _loader);

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
}
