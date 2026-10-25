using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.ViewModels;

/// <summary>Root view model: owns both panes, loads files into them and recomputes the diff.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IJsonFileLoader _loader;
    private readonly IMatchRulesLoader _rulesLoader;
    private readonly IThemeService _themes;
    private MatchRules _matchRules = MatchRules.Empty;

    public MainViewModel(IDialogService dialogs, IJsonFileLoader loader, IMatchRulesLoader rulesLoader,
        IThemeService themes)
    {
        _dialogs = dialogs;
        _loader = loader;
        _rulesLoader = rulesLoader;
        _themes = themes;
        SortKeys = true;
        Left.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FilePaneViewModel.TopRow) or nameof(FilePaneViewModel.ViewportRows))
                NotifyNavigationChanged();
        };
        Refresh();
    }

    public FilePaneViewModel Left { get; } = new("Left");

    public FilePaneViewModel Right { get; } = new("Right");

    [ObservableProperty]
    public partial bool SortKeys { get; set; }

    [ObservableProperty]
    public partial bool CompareNumbers { get; set; }

    /// <summary>The number tolerance as typed; '.' and ',' are both accepted as the decimal separator.</summary>
    [ObservableProperty]
    public partial string ToleranceText { get; set; } = "0.0005";

    /// <summary>False while <see cref="ToleranceText"/> is not a number ≥ 0; the last valid tolerance stays in effect.</summary>
    [ObservableProperty]
    public partial bool IsToleranceValid { get; private set; } = true;

    private decimal _tolerance = 0.0005m;

    /// <summary>Hide the unchanged rows away from the differences behind fold rows.</summary>
    [ObservableProperty]
    public partial bool ShowChangesOnly { get; set; }

    /// <summary>The number of unchanged rows shown around each difference, as typed.</summary>
    [ObservableProperty]
    public partial string ContextLinesText { get; set; } = "2";

    /// <summary>False while <see cref="ContextLinesText"/> is not a whole number from 0 to 99.</summary>
    [ObservableProperty]
    public partial bool IsContextLinesValid { get; private set; } = true;

    /// <summary>The last valid <see cref="ContextLinesText"/>.</summary>
    public int ContextLines { get; private set; } = 2;

    private const int MaxContextLines = 99;

    /// <summary>Show the overview bar of both sides' differences next to the panes.</summary>
    [ObservableProperty]
    public partial bool ShowOverview { get; set; } = true;

    /// <summary>The color theme; applied as soon as it changes.</summary>
    [ObservableProperty]
    public partial AppTheme Theme { get; set; } = AppTheme.System;

    partial void OnThemeChanged(AppTheme value) => _themes.Apply(value);

    [RelayCommand]
    private void SetTheme(AppTheme theme) => Theme = theme;

    private DiffResult? _result;
    private IReadOnlyList<DiffBlock> _blocks = [];
    private FoldedView? _view;

    /// <summary>The first full-diff rows of the folds the user expanded; reset whenever the folds change.</summary>
    private readonly HashSet<int> _expanded = [];

    /// <summary>The current diff's counts, or null unless both sides are loaded.</summary>
    [ObservableProperty]
    public partial DiffSummary? Summary { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    private bool HasAnyDocument => Left.HasDocument || Right.HasDocument;

    partial void OnSortKeysChanged(bool value) => Refresh();

    partial void OnCompareNumbersChanged(bool value) => Refresh();

    partial void OnToleranceTextChanged(string value)
    {
        IsToleranceValid = TryParseTolerance(value, out var tolerance);
        if (!IsToleranceValid || tolerance == _tolerance)
            return;

        _tolerance = tolerance;
        if (CompareNumbers)
            Refresh();
    }

    partial void OnShowChangesOnlyChanged(bool value) => ApplyView();

    partial void OnContextLinesTextChanged(string value)
    {
        IsContextLinesValid = int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var lines) &&
                              lines <= MaxContextLines;
        if (!IsContextLinesValid || lines == ContextLines)
            return;

        ContextLines = lines;
        _expanded.Clear();
        if (ShowChangesOnly)
            ApplyView();
        else
            NotifyNavigationChanged();
    }

    private static bool TryParseTolerance(string text, out decimal tolerance) =>
        decimal.TryParse(text.Replace(',', '.'),
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out tolerance);

    /// <summary>(Re)reads the match rules; on error the previous rules stay in effect.</summary>
    public void LoadMatchRules()
    {
        TryLoadMatchRules();
        Refresh();
    }

    /// <summary>Loads up to two paths, e.g. from the command line: the first into the left pane, the second into the right.</summary>
    public void LoadFiles(IReadOnlyList<string> paths)
    {
        if (paths.Count > 0)
            TryLoad(Left, paths[0]);
        if (paths.Count > 1)
            TryLoad(Right, paths[1]);
        Refresh();
    }

    [RelayCommand]
    private void OpenLeft() => Open(Left);

    [RelayCommand]
    private void OpenRight() => Open(Right);

    /// <summary>Dropping two files fills both panes; a single file goes into the pane it was dropped on.</summary>
    [RelayCommand]
    private void DropLeft(string[]? files) => Drop(Left, files);

    [RelayCommand]
    private void DropRight(string[]? files) => Drop(Right, files);

    [RelayCommand(CanExecute = nameof(HasAnyDocument))]
    private void Reload()
    {
        TryLoadMatchRules();
        foreach (var pane in new[] { Left, Right })
        {
            if (pane.FilePath is { } path)
                TryLoad(pane, path);
        }
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasAnyDocument))]
    private void Swap()
    {
        var (leftPath, leftDocument) = (Left.FilePath, Left.Document);
        var (rightPath, rightDocument) = (Right.FilePath, Right.Document);
        Assign(Left, rightPath, rightDocument);
        Assign(Right, leftPath, leftDocument);
        Refresh();

        static void Assign(FilePaneViewModel pane, string? path, JsonNode? document)
        {
            if (path is null)
                pane.Clear();
            else
                pane.SetDocument(path, document);
        }
    }

    [RelayCommand]
    private void ExpandFold(FoldLine? fold)
    {
        if (fold is null)
            return;

        _expanded.Add(fold.Start);
        ApplyView();
    }

    /// <summary>Scrolls the next difference below the top context rows to just below them.</summary>
    [RelayCommand(CanExecute = nameof(CanGoToNextDifference))]
    private void NextDifference()
    {
        if (NextDifferenceRow() is { } row)
            ScrollTo(Math.Max(0, row - ContextLines));
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousDifference))]
    private void PreviousDifference()
    {
        if (PreviousDifferenceRow() is { } row)
            ScrollTo(Math.Max(0, row - ContextLines));
    }

    // Once the view cannot scroll further, every difference left in that direction is already visible.
    private bool CanGoToNextDifference() =>
        NextDifferenceRow() is not null && Left.TopRow < Left.Lines.Count - Left.ViewportRows;

    private bool CanGoToPreviousDifference() => PreviousDifferenceRow() is not null && Left.TopRow > 0;

    /// <summary>The shown row a difference starts at, measured from the row below the top context rows.</summary>
    private int? NextDifferenceRow() =>
        DifferenceRows().Where(row => row > Left.TopRow + ContextLines).Cast<int?>().FirstOrDefault();

    private int? PreviousDifferenceRow() =>
        DifferenceRows().Where(row => row < Left.TopRow + ContextLines).Cast<int?>().LastOrDefault();

    private IEnumerable<int> DifferenceRows() =>
        _view is { } view ? _blocks.Select(block => view.ToShownRow(block.Start)) : [];

    private void ScrollTo(int row)
    {
        Left.TopRow = row;
        Right.TopRow = row;
    }

    private void NotifyNavigationChanged()
    {
        NextDifferenceCommand.NotifyCanExecuteChanged();
        PreviousDifferenceCommand.NotifyCanExecuteChanged();
    }

    private void Open(FilePaneViewModel pane)
    {
        var path = _dialogs.OpenJsonFile($"Open {pane.Side} File");
        if (path is null)
            return;

        TryLoad(pane, path);
        Refresh();
    }

    private void Drop(FilePaneViewModel pane, string[]? files)
    {
        if (files is null || files.Length == 0)
            return;

        if (files.Length >= 2)
        {
            LoadFiles(files);
            return;
        }

        TryLoad(pane, files[0]);
        Refresh();
    }

    private void TryLoad(FilePaneViewModel pane, string path)
    {
        try
        {
            pane.SetDocument(path, _loader.Load(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _dialogs.ShowError($"Could not load '{path}':\n\n{ex.Message}");
        }
    }

    private void TryLoadMatchRules()
    {
        try
        {
            _matchRules = _rulesLoader.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _dialogs.ShowError($"Could not load the match rules:\n\n{ex.Message}");
        }
    }

    private void Refresh()
    {
        var options = new JsonDiffOptions(SortKeys)
        {
            ArrayKeyRules = _matchRules.ArrayKeys,
            IgnoreRules = _matchRules.IgnoreKeys,
            CompareNumbers = CompareNumbers,
            NumberTolerance = _tolerance,
        };

        if (Left.HasDocument && Right.HasDocument)
        {
            _result = JsonDiffer.Diff(Left.Document, Right.Document, options);
            _blocks = DiffFolding.FindBlocks(_result);
            Summary = _result.Summary;
            StatusText = Describe(_result.Summary);
        }
        else
        {
            _result = null;
            _blocks = [];
            Left.Lines = Left.HasDocument ? [.. JsonDiffer.Format(Left.Document, options)] : [];
            Right.Lines = Right.HasDocument ? [.. JsonDiffer.Format(Right.Document, options)] : [];
            Summary = null;
            StatusText = HasAnyDocument
                ? "Open or drop a second JSON file to compare."
                : "Open or drop two JSON files to compare.";
        }

        _expanded.Clear();
        ApplyView();
        ReloadCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows the current diff in full or folded, keeping the row at the top in view.</summary>
    private void ApplyView()
    {
        var topFullRow = _view is { } old && Left.TopRow < old.FullRows.Count ? old.FullRows[Left.TopRow] : Left.TopRow;
        if (_result is null)
        {
            _view = null;
        }
        else
        {
            _view = ShowChangesOnly
                ? DiffFolding.Fold(_result, _blocks, ContextLines, _expanded)
                : DiffFolding.Unfolded(_result);
            Left.Lines = _view.Left;
            Right.Lines = _view.Right;
            ScrollTo(_view.ToContainingRow(topFullRow));
        }
        NotifyNavigationChanged();
    }

    private static string Describe(DiffSummary summary)
    {
        if (summary.IsIdentical)
            return "No differences.";

        var parts = new List<string>();
        if (summary.Modified > 0)
            parts.Add($"{summary.Modified} modified");
        if (summary.TypeChanged > 0)
            parts.Add($"{summary.TypeChanged} type changed");
        if (summary.LeftOnly > 0)
            parts.Add($"{summary.LeftOnly} only in left");
        if (summary.RightOnly > 0)
            parts.Add($"{summary.RightOnly} only in right");

        var noun = summary.Total == 1 ? "difference" : "differences";
        return $"{summary.Total} {noun}: {string.Join(", ", parts)}.";
    }
}
