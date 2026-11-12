namespace JsonDiff.Services;

/// <summary>Abstracts file/dialog interactions so the view models stay free of view types.</summary>
public interface IDialogService
{
    /// <summary>Shows an open-file dialog for a JSON file; returns the selected path, or null if canceled.</summary>
    string? OpenJsonFile(string title);

    /// <summary>Shows an error message to the user.</summary>
    void ShowError(string message);

    /// <summary>Opens a text file in the user's editor for its type.</summary>
    void OpenInEditor(string path);

    /// <summary>Shows the About dialog.</summary>
    void ShowAbout();

    /// <summary>Asks the application to shut down.</summary>
    void RequestShutdown();
}
