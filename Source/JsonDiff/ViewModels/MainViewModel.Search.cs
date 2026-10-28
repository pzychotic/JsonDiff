using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.ViewModels;

/// <summary>The search through one pane's text, shown in the find bar.</summary>
public partial class MainViewModel
{
    /// <summary>Show the find bar and the hits; closing keeps the text, so the search can be resumed.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseSearchCommand))]
    public partial bool IsSearchOpen { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindNextCommand), nameof(FindPreviousCommand))]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool SearchMatchCase { get; set; }

    [ObservableProperty]
    public partial bool SearchWholeWord { get; set; }

    /// <summary>The pane searched through.</summary>
    [ObservableProperty]
    public partial PaneSide SearchSide { get; set; }

    /// <summary>"3 of 17", "No matches", or empty while there is nothing to search for.</summary>
    [ObservableProperty]
    public partial string SearchStatus { get; private set; } = "";

    /// <summary>True while there is text to search for but no hit.</summary>
    [ObservableProperty]
    public partial bool HasNoMatches { get; private set; }

    /// <summary>The hits in the searched side's full-diff rows, in order.</summary>
    private IReadOnlyList<SearchMatch> _matches = [];

    /// <summary>The index into <see cref="_matches"/> of the hit navigated to.</summary>
    private int? _currentMatch;

    partial void OnIsSearchOpenChanged(bool value) => RunSearch(jump: value);

    partial void OnSearchTextChanged(string value) => RunSearch(jump: true);

    partial void OnSearchMatchCaseChanged(bool value) => RunSearch(jump: true);

    partial void OnSearchWholeWordChanged(bool value) => RunSearch(jump: true);

    partial void OnSearchSideChanged(PaneSide value) => RunSearch(jump: true);

    [RelayCommand(CanExecute = nameof(IsSearchOpen))]
    private void CloseSearch() => IsSearchOpen = false;

    /// <summary>Goes to the next hit, after the last one back to the first; reopens a closed search instead.</summary>
    [RelayCommand(CanExecute = nameof(HasSearchText))]
    private void FindNext() => Find(+1);

    [RelayCommand(CanExecute = nameof(HasSearchText))]
    private void FindPrevious() => Find(-1);

    private bool HasSearchText() => SearchText.Length > 0;

    private void Find(int step)
    {
        // Opening searches anew and goes to the first hit in view.
        if (!IsSearchOpen)
        {
            IsSearchOpen = true;
            return;
        }

        if (_matches.Count == 0)
            return;

        GoToMatch(_currentMatch is { } current
            ? (current + step + _matches.Count) % _matches.Count
            : FirstMatchFrom(Left.TopRow));
    }

    /// <summary>
    /// Finds the hits anew and makes the first one at or below the top row current; <paramref name="jump"/> scrolls
    /// to it.
    /// </summary>
    private void RunSearch(bool jump)
    {
        _matches = IsSearchOpen
            ? TextSearch.Find(SearchedRows(), new SearchQuery(SearchText, SearchMatchCase, SearchWholeWord))
            : [];
        _currentMatch = _matches.Count > 0 ? FirstMatchFrom(Left.TopRow) : null;
        if (jump && _currentMatch is { } current)
            GoToMatch(current);
        else
            ShowSearch();
    }

    /// <summary>Forgets the hits of rows about to be replaced; <see cref="RunSearch"/> finds them anew.</summary>
    private void ResetSearch()
    {
        _matches = [];
        _currentMatch = null;
    }

    /// <summary>The searched side's rows: the full diff, folded rows included, or the lone document's lines.</summary>
    private IReadOnlyList<DiffLine> SearchedRows()
    {
        if (_result is { } result)
            return SearchSide == PaneSide.Left ? result.Left : result.Right;

        return [.. SearchedPane.Lines.OfType<DiffLine>()];
    }

    private FilePaneViewModel SearchedPane => SearchSide == PaneSide.Left ? Left : Right;

    /// <summary>The shown row holding full row <paramref name="fullRow"/>, which may be a fold.</summary>
    private int ShownRow(int fullRow) => _view?.ToContainingRow(fullRow) ?? fullRow;

    /// <summary>The first hit shown at or below <paramref name="row"/>; the first hit if there is none below.</summary>
    private int FirstMatchFrom(int row)
    {
        for (var index = 0; index < _matches.Count; index++)
        {
            if (ShownRow(_matches[index].Row) >= row)
                return index;
        }
        return 0;
    }

    /// <summary>Makes a hit current, shows the fold hiding it, and scrolls it to the middle unless it is in view.</summary>
    private void GoToMatch(int index)
    {
        _currentMatch = index;
        var fullRow = _matches[index].Row;
        if (_view is { } view && view.Left[view.ToContainingRow(fullRow)] is FoldLine fold)
        {
            _expanded.Add(fold.Start);
            ApplyView();
        }

        var row = ShownRow(fullRow);
        if (row < Left.TopRow || row >= Left.TopRow + Left.ViewportRows)
            ScrollTo(Math.Max(0, row - Left.ViewportRows / 2));
        ShowSearch();
    }

    /// <summary>Hands the hits to the searched pane and its rows to the overview; the other pane shows none.</summary>
    private void ShowSearch()
    {
        var (searched, other) = SearchSide == PaneSide.Left ? (Left, Right) : (Right, Left);
        other.SearchHighlights = null;
        other.SearchRows = [];

        if (!IsSearchOpen)
        {
            searched.SearchHighlights = null;
            searched.SearchRows = [];
            SearchStatus = "";
            HasNoMatches = false;
            return;
        }

        var rows = SearchedRows();
        searched.SearchHighlights = new SearchHighlights(_matches.Select((match, index) =>
            (rows[match.Row], new TextHighlight(match.Start, match.Length, index == _currentMatch))));
        searched.SearchRows = [.. _matches.Select(match => ShownRow(match.Row)).Distinct()];

        HasNoMatches = SearchText.Length > 0 && _matches.Count == 0;
        SearchStatus = SearchText.Length == 0 ? ""
            : _currentMatch is { } current ? $"{current + 1} of {_matches.Count}"
            : "No matches";
    }
}
