using JsonDiff.Core.Models;

namespace JsonDiff.Core.Services;

public interface IMatchRulesLoader
{
    /// <summary>The configured rules; <see cref="MatchRules.Empty"/> when none are configured.</summary>
    /// <exception cref="System.Text.Json.JsonException">The rules file is invalid.</exception>
    MatchRules Load();

    /// <summary>
    /// The path of the rules file in effect; when there is none, the user's file is created from the shipped example
    /// first.
    /// </summary>
    /// <exception cref="System.IO.IOException">The file could not be created.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be created.</exception>
    string GetOrCreateFile();
}
