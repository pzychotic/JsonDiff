using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using JsonDiff.Models;

namespace JsonDiff.ViewModels;

/// <summary>One side of the comparison: the loaded file, its parsed document and the lines to display.</summary>
public partial class FilePaneViewModel(string side) : ObservableObject
{
    /// <summary>"Left" or "Right", for headers and dialog titles.</summary>
    public string Side { get; } = side;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName), nameof(HasDocument))]
    public partial string? FilePath { get; private set; }

    public string FileName => FilePath is null ? $"{Side}: no file" : Path.GetFileName(FilePath);

    public bool HasDocument => FilePath is not null;

    /// <summary>The parsed document; only meaningful while <see cref="HasDocument"/> is true.</summary>
    public JsonNode? Document { get; private set; }

    /// <summary>The rows to display, each a <see cref="DiffLine"/> or a <see cref="FoldLine"/>.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> Lines { get; set; } = [];

    /// <summary>The row shown at the top; set by scrolling the view, and setting it scrolls the view.</summary>
    [ObservableProperty]
    public partial int TopRow { get; set; }

    /// <summary>How many whole rows fit into the view; set by the view.</summary>
    [ObservableProperty]
    public partial int ViewportRows { get; set; }

    /// <summary>The search hits to highlight in the rows' text, or null while this pane is not searched.</summary>
    [ObservableProperty]
    public partial SearchHighlights? SearchHighlights { get; set; }

    /// <summary>The shown rows holding a search hit, ascending; a hit in a fold counts for the fold's row.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<int> SearchRows { get; set; } = [];

    public void SetDocument(string path, JsonNode? document)
    {
        Document = document;
        FilePath = path;
    }

    public void Clear()
    {
        Document = null;
        FilePath = null;
    }
}
