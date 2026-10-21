using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonDiff.Models;

namespace JsonDiff.Services;

/// <summary>
/// Reads <see cref="MatchRules"/> from the first of the candidate files that exists. The file is lenient JSON
/// (comments and trailing commas allowed):
/// <code>
/// {
///   "ArrayKeys": [ { "When": { "Type": ["A", "B"] }, "Key": ["Type", "Fields[Name=Id].Value"] } ],
///   "IgnoreKeys": [ "Timestamp", { "When": { "Type": "A" }, "Keys": ["Revision"] } ]
/// }
/// </code>
/// A condition's value is a JSON value, or an array of alternatives; paths use the <see cref="JsonKeyPath"/> syntax.
/// An ignore rule given as a string ignores that property name everywhere.
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

    public MatchRules Load()
    {
        var path = candidatePaths.FirstOrDefault(File.Exists);
        if (path is null)
            return MatchRules.Empty;

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new JsonException($"{path}: {ex.Message}", ex);
        }
    }

    internal static MatchRules Parse(string json)
    {
        if (JsonNode.Parse(json, documentOptions: JsonFileLoader.DocumentOptions) is not JsonObject root)
            throw new JsonException("The match rules must be a JSON object.");
        // Catch typos, which would otherwise silently drop a whole section.
        if (root.Select(p => p.Key).FirstOrDefault(name => name is not ("ArrayKeys" or "IgnoreKeys")) is { } unknown)
            throw new JsonException(
                $"The match rules have an unknown property \"{unknown}\"; expected \"ArrayKeys\" and \"IgnoreKeys\".");

        return new MatchRules(
            ParseRules(root, "ArrayKeys", ParseArrayKeyRule),
            ParseRules(root, "IgnoreKeys", ParseIgnoreRule));
    }

    private static List<T> ParseRules<T>(JsonObject root, string name, Func<JsonNode?, int, T> parseRule) =>
        root[name] switch
        {
            null => [],
            JsonArray rules => [.. rules.Select(parseRule)],
            _ => throw new JsonException($"\"{name}\" must be an array of rules."),
        };

    private static ArrayKeyRule ParseArrayKeyRule(JsonNode? node, int index)
    {
        var where = $"ArrayKeys[{index}]";
        var rule = RuleObject(node, where, "Key");

        if (rule["Key"] is not JsonArray { Count: > 0 } key || !AllStrings(key))
            throw new JsonException($"{where}.Key must be a non-empty array of paths.");

        return new ArrayKeyRule(
            [.. key.Select(k => ParsePath(k!.GetValue<string>(), $"{where}.Key"))], ParseConditions(rule, where));
    }

    private static IgnoreRule ParseIgnoreRule(JsonNode? node, int index)
    {
        var where = $"IgnoreKeys[{index}]";
        if (node?.GetValueKind() == JsonValueKind.String)
            return new IgnoreRule([node.GetValue<string>()], []);
        var rule = RuleObject(node, where, "Keys");

        if (rule["Keys"] is not JsonArray { Count: > 0 } keys || !AllStrings(keys))
            throw new JsonException($"{where}.Keys must be a non-empty array of property names.");

        return new IgnoreRule([.. keys.Select(k => k!.GetValue<string>())], ParseConditions(rule, where));
    }

    /// <summary>The rule as an object that has no properties besides "When" and <paramref name="keyProperty"/>.</summary>
    private static JsonObject RuleObject(JsonNode? node, string where, string keyProperty)
    {
        if (node is not JsonObject rule)
            throw new JsonException($"{where} must be an object.");
        // Catch typos, which would otherwise silently drop a condition or key.
        if (rule.Select(p => p.Key).FirstOrDefault(name => name != "When" && name != keyProperty) is { } unknown)
            throw new JsonException(
                $"{where} has an unknown property \"{unknown}\"; expected \"When\" and \"{keyProperty}\".");
        return rule;
    }

    private static List<KeyCondition> ParseConditions(JsonObject rule, string where) => rule["When"] switch
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

    private static bool AllStrings(JsonArray array) => array.All(k => k?.GetValueKind() == JsonValueKind.String);

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
