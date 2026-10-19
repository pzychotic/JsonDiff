namespace JsonDiff.Models;

/// <summary>
/// State remembered between application runs (window placement).
/// Null properties mean "never saved" and leave the built-in defaults in effect.
/// </summary>
public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
}
