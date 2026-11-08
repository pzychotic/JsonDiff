using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Cli;

/// <summary>Command line front end for the diff engine in JsonDiff.Core.</summary>
public static class CliApp
{
    /// <summary>Parses <paramref name="args"/>, runs the command and returns the process exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var result = BuildRootCommand(output, error).Parse(args);
        var config = new InvocationConfiguration { Output = output, Error = error };

        // System.CommandLine reports parse errors with exit code 1, which here means "different".
        if (result.Errors.Count > 0 && !IsHelpOrVersion(result))
        {
            foreach (var parseError in result.Errors)
                error.WriteLine(parseError.Message);
            error.WriteLine("Run with --help for usage.");
            return ExitCodes.Error;
        }

        return result.Invoke(config);
    }

    private static bool IsHelpOrVersion(ParseResult result) =>
        result.Tokens.Any(t => t.Value is "--help" or "-h" or "-?" or "--version");

    private static RootCommand BuildRootCommand(TextWriter output, TextWriter error)
    {
        var root = new RootCommand(
            "Compare two JSON files structurally. Exit code: 0 identical, 1 different, 2 error.");
        root.Subcommands.Add(BuildStatsCommand(output, error));
        return root;
    }

    private static Command BuildStatsCommand(TextWriter output, TextWriter error)
    {
        var left = new Argument<FileInfo>("left") { Description = "The left JSON file." }.AcceptExistingOnly();
        var right = new Argument<FileInfo>("right") { Description = "The right JSON file." }.AcceptExistingOnly();

        var rules = new Option<FileInfo>("--rules", "-r")
        {
            Description = $"Match rules file. Default: %APPDATA%\\JsonDiff\\{MatchRulesFileLoader.FileName}, " +
                          "else the one next to the executable.",
        }.AcceptExistingOnly();
        var noRules = new Option<bool>("--no-rules") { Description = "Ignore all match rules." };
        var compareNumbers = new Option<bool>("--compare-numbers", "-n")
        {
            Description = "Compare numbers and numeric strings by value.",
        };
        // No DefaultValueFactory: help would print the default in the current culture ("0,0005").
        var tolerance = new Option<decimal?>("--tolerance", "-t")
        {
            Description = "Largest difference at which two numbers are still equal (with --compare-numbers). " +
                          $"Default: {JsonDiffOptions.Default.NumberTolerance.ToString(CultureInfo.InvariantCulture)}.",
            HelpName = "value",
            CustomParser = ParseTolerance,
        };
        var json = new Option<bool>("--json") { Description = "Write the stats as JSON." };

        var command = new Command("stats", "Show how many differences two JSON files have, by kind.")
        {
            left, right, rules, noRules, compareNumbers, tolerance, json,
        };
        command.Validators.Add(r =>
        {
            if (r.GetResult(rules) is not null && r.GetValue(noRules))
                r.AddError("--rules and --no-rules cannot be combined.");
        });

        command.SetAction(parseResult =>
        {
            var request = new StatsRequest(
                parseResult.GetValue(left)!.FullName,
                parseResult.GetValue(right)!.FullName,
                parseResult.GetValue(noRules) ? null : RulesLoader(parseResult.GetValue(rules)),
                parseResult.GetValue(compareNumbers),
                parseResult.GetValue(tolerance) ?? JsonDiffOptions.Default.NumberTolerance,
                parseResult.GetValue(json));
            return StatsCommand.Run(request, new JsonFileLoader(), output, error);
        });
        return command;
    }

    private static MatchRulesFileLoader RulesLoader(FileInfo? file) =>
        file is null ? new MatchRulesFileLoader() : new MatchRulesFileLoader([file.FullName]);

    // Always invariant, so "0.001" means the same on every machine.
    private static decimal? ParseTolerance(ArgumentResult result)
    {
        var text = result.Tokens.Single().Value;
        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return value;

        result.AddError($"Invalid tolerance \"{text}\"; expected a non-negative number such as 0.001.");
        return null;
    }
}

/// <param name="Rules">Where to read the match rules from; null for none.</param>
public sealed record StatsRequest(
    string LeftPath, string RightPath, IMatchRulesLoader? Rules, bool CompareNumbers, decimal Tolerance, bool Json);

/// <summary>Diffs two files and prints their <see cref="DiffSummary"/>.</summary>
public static class StatsCommand
{
    private static readonly JsonSerializerOptions JsonOutput = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static int Run(StatsRequest request, IJsonFileLoader loader, TextWriter output, TextWriter error)
    {
        DiffSummary summary;
        try
        {
            var rules = request.Rules?.Load() ?? MatchRules.Empty;
            var options = new JsonDiffOptions
            {
                ArrayKeyRules = rules.ArrayKeys,
                IgnoreRules = rules.IgnoreKeys,
                CompareNumbers = request.CompareNumbers,
                NumberTolerance = request.Tolerance,
            };
            summary = JsonDiffer.Diff(Load(loader, request.LeftPath), Load(loader, request.RightPath), options).Summary;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }

        if (request.Json)
            WriteJson(output, request, summary);
        else
            WriteText(output, request, summary);

        return summary.IsIdentical ? ExitCodes.Identical : ExitCodes.Different;
    }

    private static System.Text.Json.Nodes.JsonNode? Load(IJsonFileLoader loader, string path)
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

    private static void WriteText(TextWriter output, StatsRequest request, DiffSummary summary)
    {
        output.WriteLine($"Left:  {request.LeftPath}");
        output.WriteLine($"Right: {request.RightPath}");
        output.WriteLine();
        output.WriteLine($"Modified       {summary.Modified,6}");
        output.WriteLine($"Type changed   {summary.TypeChanged,6}");
        output.WriteLine($"Only in left   {summary.LeftOnly,6}");
        output.WriteLine($"Only in right  {summary.RightOnly,6}");
        output.WriteLine($"Total          {summary.Total,6}");
        output.WriteLine();
        output.WriteLine(summary.Describe());
    }

    private static void WriteJson(TextWriter output, StatsRequest request, DiffSummary summary)
    {
        var stats = new
        {
            left = request.LeftPath,
            right = request.RightPath,
            identical = summary.IsIdentical,
            summary.Modified,
            summary.TypeChanged,
            summary.LeftOnly,
            summary.RightOnly,
            summary.Total,
        };
        output.WriteLine(JsonSerializer.Serialize(stats, JsonOutput));
    }
}
