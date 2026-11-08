using Microsoft.Win32;
using System.Windows;
using JsonDiff.Views;

namespace JsonDiff.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/> using Win32 common dialogs.</summary>
public sealed class WpfDialogService(Window owner, ThemeService themes) : IDialogService
{
    public string? OpenJsonFile(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "JSON files|*.json|All files|*.*",
            DefaultExt = ".json",
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public void ShowError(string message) =>
        MessageBox.Show(owner, message, "JsonDiff", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowAbout()
    {
        var about = new AboutWindow { Owner = owner };
        // The first moment the window has a handle, before it is shown; later theme switches update it too.
        about.SourceInitialized += (_, _) => themes.UpdateTitleBar(about);
        about.ShowDialog();
    }

    public void RequestShutdown() => Application.Current.Shutdown();
}
