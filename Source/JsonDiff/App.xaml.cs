using System.Globalization;
using System.Windows;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ThemeService? _themes;

    protected override void OnStartup(StartupEventArgs e)
    {
        var invariant = CultureInfo.InvariantCulture;

        // Ensure the current thread uses invariant culture
        Thread.CurrentThread.CurrentCulture = invariant;
        Thread.CurrentThread.CurrentUICulture = invariant;

        // Ensure future threads default to invariant culture
        CultureInfo.DefaultThreadCurrentCulture = invariant;
        CultureInfo.DefaultThreadCurrentUICulture = invariant;

        base.OnStartup(e);

        // composition root: construct services and inject them into the main window
        var settingsService = new JsonSettingsService();
        var settings = settingsService.Load() ?? new AppSettings();

        // Before the window exists, so it never shows in the wrong colors.
        _themes = new ThemeService(this);
        _themes.Apply(settings.Theme ?? AppTheme.System);

        // Command line: JsonDiff.exe [left.json [right.json]]
        var window = new MainWindow(settingsService, _themes, settings, e.Args);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _themes?.Dispose();
        base.OnExit(e);
    }
}
