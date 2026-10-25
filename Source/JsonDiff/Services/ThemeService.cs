using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using JsonDiff.Models;
using JsonDiff.Themes;

namespace JsonDiff.Services;

/// <summary>
/// Applies a theme by swapping the palette dictionary in the application resources; the brushes over its colors are
/// dynamic, so every window recolors at once. With <see cref="AppTheme.System"/> it follows the Windows app mode.
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    /// <summary>The palette of each concrete theme; a new theme needs a palette file and an entry here.</summary>
    private static readonly Dictionary<AppTheme, Uri> Palettes = new()
    {
        [AppTheme.Light] = new("pack://application:,,,/Themes/Light.xaml"),
        [AppTheme.Dark] = new("pack://application:,,,/Themes/Dark.xaml"),
    };

    /// <summary>A key every palette defines; tells palettes apart from the other merged dictionaries.</summary>
    private const string IsDarkThemeKey = "IsDarkTheme";

    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Application _app;
    private ResourceDictionary _palette;
    private AppTheme _selected = AppTheme.System;
    private bool _followingSystem;

    /// <summary>The theme of <see cref="_palette"/>; App.xaml starts out with the light one.</summary>
    private AppTheme _applied = AppTheme.Light;

    public ThemeService(Application app)
    {
        _app = app;
        _palette = app.Resources.MergedDictionaries.First(d => d.Contains(IsDarkThemeKey));
    }

    private bool IsDark => _palette[IsDarkThemeKey] is true;

    public void Apply(AppTheme theme)
    {
        _selected = theme;
        FollowSystem(theme == AppTheme.System);
        Update();
    }

    /// <summary>Gives <paramref name="window"/> a title bar matching the theme; call once its handle exists.</summary>
    public void UpdateTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var dark = IsDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
    }

    public void Dispose() => FollowSystem(false);

    private void Update()
    {
        var theme = _selected == AppTheme.System ? SystemTheme() : _selected;
        if (theme == _applied)
            return;

        var palette = new ResourceDictionary { Source = Palettes[theme] };
        var dictionaries = _app.Resources.MergedDictionaries;
        dictionaries[dictionaries.IndexOf(_palette)] = palette;
        (_palette, _applied) = (palette, theme);

        foreach (Window window in _app.Windows)
            UpdateTitleBar(window);
    }

    /// <summary>The Windows app mode; light when it can't be read.</summary>
    private static AppTheme SystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is 0 ? AppTheme.Dark : AppTheme.Light;
    }

    private void FollowSystem(bool follow)
    {
        if (follow == _followingSystem)
            return;

        if (follow)
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        else
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _followingSystem = follow;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Switching the app mode raises General; the event may come on another thread.
        if (e.Category == UserPreferenceCategory.General)
            _app.Dispatcher.BeginInvoke(Update);
    }
}
