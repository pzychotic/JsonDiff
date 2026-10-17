using JsonDiff.Services;

namespace JsonDiff.Tests.TestSupport;

/// <summary>Scriptable <see cref="IDialogService"/> for driving <c>MainViewModel</c> without a UI.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public string? OpenResult { get; set; }
    public List<string> OpenTitles { get; } = [];
    public List<string> Errors { get; } = [];

    public string? OpenJsonFile(string title)
    {
        OpenTitles.Add(title);
        return OpenResult;
    }

    public void ShowError(string message) => Errors.Add(message);
}
