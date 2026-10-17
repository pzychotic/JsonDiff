using Microsoft.Win32;
using System.Windows;

namespace JsonDiff.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/> using Win32 common dialogs.</summary>
public sealed class WpfDialogService(Window owner) : IDialogService
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
}
