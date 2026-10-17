namespace JsonDiff.Services;

/// <summary>Abstracts file/dialog interactions so the view models stay free of view types.</summary>
public interface IDialogService
{
    /// <summary>Shows an open-file dialog for a JSON file; returns the selected path, or null if canceled.</summary>
    string? OpenJsonFile(string title);

    /// <summary>Shows an error message to the user.</summary>
    void ShowError(string message);
}
