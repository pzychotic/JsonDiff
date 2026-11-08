using JsonDiff.Core.Models;

namespace JsonDiff.Core.Services;

public interface IMatchRulesLoader
{
    /// <summary>The configured rules; <see cref="MatchRules.Empty"/> when none are configured.</summary>
    /// <exception cref="System.Text.Json.JsonException">The rules file is invalid.</exception>
    MatchRules Load();
}
