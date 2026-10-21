using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private MatchRules _matchRules = MatchRules.Empty;

    public MainViewModel(IDialogService dialogs, IJsonFileLoader loader, IMatchRulesLoader rulesLoader)
    {
        _dialogs = dialogs;
        _loader = loader;
        _rulesLoader = rulesLoader;
        SortKeys = true;
        Refresh();
    }

    public FilePaneViewModel Left { get; } = new("Left");

    public FilePaneViewModel Right { get; } = new("Right");

    [ObservableProperty]
    public partial bool SortKeys { get; set; }

    /// <summary>The current diff's counts, or null unless both sides are loaded.</summary>
    [ObservableProperty]
    public partial DiffSummary? Summary { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    private bool HasAnyDocument => Left.HasDocument || Right.HasDocument;

    partial void OnSortKeysChanged(bool value) => Refresh();

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
        };

        if (Left.HasDocument && Right.HasDocument)
        {
            var result = JsonDiffer.Diff(Left.Document, Right.Document, options);
            Left.Lines = result.Left;
            Right.Lines = result.Right;
            Summary = result.Summary;
            StatusText = Describe(result.Summary);
        }
        else
        {
            Left.Lines = Left.HasDocument ? JsonDiffer.Format(Left.Document, options) : [];
            Right.Lines = Right.HasDocument ? JsonDiffer.Format(Right.Document, options) : [];
            Summary = null;
            StatusText = HasAnyDocument
                ? "Open or drop a second JSON file to compare."
                : "Open or drop two JSON files to compare.";
        }

        ReloadCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
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
