using JsonDiff.Models;

namespace JsonDiff.Services;

public interface IMatchRulesLoader
{
    /// <summary>The configured rules; <see cref="MatchRules.Empty"/> when none are configured.</summary>
    /// <exception cref="System.Text.Json.JsonException">The rules file is invalid.</exception>
    MatchRules Load();
}
