using JsonDiff.Models;

namespace JsonDiff.Services;

public interface IMatchRulesLoader
{
    /// <summary>The configured array key rules; empty when none are configured.</summary>
    /// <exception cref="System.Text.Json.JsonException">The rules file is invalid.</exception>
    IReadOnlyList<ArrayKeyRule> Load();
}
