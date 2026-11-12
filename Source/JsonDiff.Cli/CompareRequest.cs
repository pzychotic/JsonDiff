using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Cli;

/// <summary>The two files to compare and how to compare them; shared by all commands.</summary>
/// <param name="Rules">Where to read the match rules from; null for none.</param>
public sealed record CompareRequest(
    string LeftPath, string RightPath, IMatchRulesLoader? Rules, bool CompareNumbers, decimal Tolerance)
{
    /// <summary>Loads both files and the rules, and diffs them.</summary>
    /// <exception cref="Exception">Any exception for which <see cref="IsUserError"/> is true.</exception>
    public DiffResult Diff(IJsonFileLoader loader)
    {
        var rules = Rules?.Load() ?? MatchRules.Empty;
        var options = new JsonDiffOptions
        {
            ArrayKeyRules = rules.ArrayKeys,
            IgnoreRules = rules.IgnoreKeys,
            CompareNumbers = CompareNumbers,
            NumberTolerance = Tolerance,
        };
        return JsonDiffer.Diff(Load(loader, LeftPath), Load(loader, RightPath), options);
    }

    /// <summary>
    /// True for the exceptions of <see cref="Diff"/> that are reported as an error message rather than a crash: an
    /// unreadable file, or invalid JSON or match rules.
    /// </summary>
    public static bool IsUserError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or JsonException;

    private static JsonNode? Load(IJsonFileLoader loader, string path)
    {
        try
        {
            return loader.Load(path);
        }
        catch (JsonException ex)
        {
            throw new JsonException($"{path}: {ex.Message}", ex);
        }
    }
}

/// <summary>Settings for the commands' <c>--json</c> output.</summary>
internal static class JsonOutput
{
    /// <summary>
    /// Indented camelCase. The relaxed encoder keeps the JSON text in the output readable (no <c>"</c> for
    /// quotes); the output is never embedded in HTML.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
