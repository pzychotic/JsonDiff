using System.Text.Json.Serialization;

namespace JsonDiff.Models;

/// <summary>The color theme chosen in View ▸ Theme; saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme
{
    /// <summary>Light or dark, following the Windows app mode, also while running.</summary>
    System,

    Light,

    Dark,
}
