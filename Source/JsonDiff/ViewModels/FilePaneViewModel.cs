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

    [ObservableProperty]
    public partial IReadOnlyList<DiffLine> Lines { get; set; } = [];

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
