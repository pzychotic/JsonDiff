using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>Switches the application's colors, so the view models stay free of resource dictionaries.</summary>
public interface IThemeService
{
    /// <summary>Recolors all windows with <paramref name="theme"/>.</summary>
    void Apply(AppTheme theme);
}
