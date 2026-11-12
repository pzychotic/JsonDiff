using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
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
        root.Subcommands.Add(BuildDiffCommand(output, error));
        return root;
    }

    private static Command BuildStatsCommand(TextWriter output, TextWriter error)
    {
        var compare = new CompareOptions();
        var json = new Option<bool>("--json") { Description = "Write the stats as JSON." };

        var command = new Command("stats", "Show how many differences two JSON files have, by kind.") { json };
        compare.AddTo(command);
        command.SetAction(parseResult =>
        {
            var request = new StatsRequest(compare.GetRequest(parseResult), parseResult.GetValue(json));
            return StatsCommand.Run(request, new JsonFileLoader(), output, error);
        });
        return command;
    }

    private static Command BuildDiffCommand(TextWriter output, TextWriter error)
    {
        var compare = new CompareOptions();
        var context = new Option<int>("--context", "-U")
        {
            Description = "Lines to show before and after each difference.",
            HelpName = "lines",
            DefaultValueFactory = _ => DiffCommand.DefaultContext,
        };
        context.Validators.Add(r =>
        {
            if (r.GetValueOrDefault<int>() < 0)
                r.AddError("--context cannot be negative.");
        });
        var json = new Option<bool>("--json")
        {
            Description = "Write the hunks as JSON, with the kind and line number of each line on both sides.",
        };

        var command = new Command("diff",
            "Show the differing parts of two JSON files as a unified diff of their pretty-printed text.")
        {
            context, json,
        };
        compare.AddTo(command);
        command.SetAction(parseResult =>
        {
            var request = new DiffRequest(
                compare.GetRequest(parseResult), parseResult.GetValue(context), parseResult.GetValue(json));
            return DiffCommand.Run(request, new JsonFileLoader(), output, error);
        });
        return command;
    }

    /// <summary>The arguments and options that choose the files and how to compare them, in every command.</summary>
    private sealed class CompareOptions
    {
        private readonly Argument<FileInfo> _left =
            new Argument<FileInfo>("left") { Description = "The left JSON file." }.AcceptExistingOnly();

        private readonly Argument<FileInfo> _right =
            new Argument<FileInfo>("right") { Description = "The right JSON file." }.AcceptExistingOnly();

        private readonly Option<FileInfo> _rules = new Option<FileInfo>("--rules", "-r")
        {
            Description = $"Match rules file. Default: %APPDATA%\\JsonDiff\\{MatchRulesFileLoader.FileName}, " +
                          "else the one next to the executable.",
        }.AcceptExistingOnly();

        private readonly Option<bool> _noRules = new("--no-rules") { Description = "Ignore all match rules." };

        private readonly Option<bool> _compareNumbers = new("--compare-numbers", "-n")
        {
            Description = "Compare numbers and numeric strings by value.",
        };

        // No DefaultValueFactory: help would print the default in the current culture ("0,0005").
        private readonly Option<decimal?> _tolerance = new("--tolerance", "-t")
        {
            Description = "Largest difference at which two numbers are still equal (with --compare-numbers). " +
                          $"Default: {JsonDiffOptions.Default.NumberTolerance.ToString(CultureInfo.InvariantCulture)}.",
            HelpName = "value",
            CustomParser = ParseTolerance,
        };

        public void AddTo(Command command)
        {
            command.Arguments.Add(_left);
            command.Arguments.Add(_right);
            command.Options.Add(_rules);
            command.Options.Add(_noRules);
            command.Options.Add(_compareNumbers);
            command.Options.Add(_tolerance);
            command.Validators.Add(r =>
            {
                if (r.GetResult(_rules) is not null && r.GetValue(_noRules))
                    r.AddError("--rules and --no-rules cannot be combined.");
            });
        }

        public CompareRequest GetRequest(ParseResult parseResult) => new(
            parseResult.GetValue(_left)!.FullName,
            parseResult.GetValue(_right)!.FullName,
            parseResult.GetValue(_noRules) ? null : RulesLoader(parseResult.GetValue(_rules)),
            parseResult.GetValue(_compareNumbers),
            parseResult.GetValue(_tolerance) ?? JsonDiffOptions.Default.NumberTolerance);

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
}
