using System.Text.Json;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Cli;

public sealed record StatsRequest(CompareRequest Compare, bool Json);

/// <summary>Diffs two files and prints their <see cref="DiffSummary"/>.</summary>
public static class StatsCommand
{
    public static int Run(StatsRequest request, IJsonFileLoader loader, TextWriter output, TextWriter error)
    {
        DiffSummary summary;
        try
        {
            summary = request.Compare.Diff(loader).Summary;
        }
        catch (Exception ex) when (CompareRequest.IsUserError(ex))
        {
            error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }

        if (request.Json)
            WriteJson(output, request.Compare, summary);
        else
            WriteText(output, request.Compare, summary);

        return summary.IsIdentical ? ExitCodes.Identical : ExitCodes.Different;
    }

    private static void WriteText(TextWriter output, CompareRequest request, DiffSummary summary)
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

    private static void WriteJson(TextWriter output, CompareRequest request, DiffSummary summary)
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
        output.WriteLine(JsonSerializer.Serialize(stats, JsonOutput.Options));
    }
}
