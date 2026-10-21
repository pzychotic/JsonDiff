using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>
/// Reads <see cref="ArrayKeyRule"/>s from the first of the candidate files that exists. The file is lenient JSON
/// (comments and trailing commas allowed):
/// <code>
/// { "ArrayKeys": [ { "When": { "Type": ["A", "B"] }, "Key": ["Type", "Fields[Name=Id].Value"] } ] }
/// </code>
/// A condition's value is a JSON value, or an array of alternatives; paths use the <see cref="JsonKeyPath"/> syntax.
/// </summary>
public sealed class MatchRulesFileLoader(IReadOnlyList<string> candidatePaths) : IMatchRulesLoader
{
    public const string FileName = "MatchRules.json";

    /// <summary>The user's file in the roaming profile overrides the one shipped next to the executable.</summary>
    public MatchRulesFileLoader()
        : this([
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JsonDiff", FileName),
            Path.Combine(AppContext.BaseDirectory, FileName),
        ])
    {
    }

    public IReadOnlyList<ArrayKeyRule> Load()
    {
        var path = candidatePaths.FirstOrDefault(File.Exists);
        if (path is null)
            return [];

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new JsonException($"{path}: {ex.Message}", ex);
        }
    }

    internal static List<ArrayKeyRule> Parse(string json)
    {
        if (JsonNode.Parse(json, documentOptions: JsonFileLoader.DocumentOptions) is not JsonObject root)
            throw new JsonException("The match rules must be a JSON object.");
        return root["ArrayKeys"] switch
        {
            null => [],
            JsonArray rules => [.. rules.Select(ParseRule)],
            _ => throw new JsonException("\"ArrayKeys\" must be an array of rules."),
        };
    }

    private static ArrayKeyRule ParseRule(JsonNode? node, int index)
    {
        var where = $"ArrayKeys[{index}]";
        if (node is not JsonObject rule)
            throw new JsonException($"{where} must be an object.");
        // Catch typos, which would otherwise silently drop a condition or key.
        if (rule.Select(p => p.Key).FirstOrDefault(name => name is not ("When" or "Key")) is { } unknown)
            throw new JsonException($"{where} has an unknown property \"{unknown}\"; expected \"When\" and \"Key\".");

        if (rule["Key"] is not JsonArray { Count: > 0 } key || key.Any(k => k?.GetValueKind() != JsonValueKind.String))
            throw new JsonException($"{where}.Key must be a non-empty array of paths.");

        List<KeyCondition> when = rule["When"] switch
        {
            null => [],
            JsonObject conditions =>
            [
                .. conditions.Select(c => new KeyCondition(
                    ParsePath(c.Key, $"{where}.When"),
                    c.Value is JsonArray alternatives ? [.. alternatives] : [c.Value])),
            ],
            _ => throw new JsonException($"{where}.When must be an object of path/value conditions."),
        };

        return new ArrayKeyRule([.. key.Select(k => ParsePath(k!.GetValue<string>(), $"{where}.Key"))], when);
    }

    private static JsonKeyPath ParsePath(string text, string where)
    {
        try
        {
            return JsonKeyPath.Parse(text);
        }
        catch (FormatException ex)
        {
            throw new JsonException($"{where}: {ex.Message}", ex);
        }
    }
}
