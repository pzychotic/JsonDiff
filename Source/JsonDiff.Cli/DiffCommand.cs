using System.Text.Json;
using JsonDiff.Core.Models;
using JsonDiff.Core.Services;

namespace JsonDiff.Cli;

/// <param name="Context">Rows to show before and after each difference.</param>
public sealed record DiffRequest(CompareRequest Compare, int Context, bool Json);

/// <summary>
/// Diffs two files and prints the differing parts as hunks: as a unified diff, or as JSON with the aligned rows of
/// both sides. Text is that of the pretty-printed files, as the GUI shows them; every line carries both its display
/// line (in the pretty-printed file) and its source line (in the file as written), so a difference can be found in
/// either.
/// </summary>
public static class DiffCommand
{
    public const int DefaultContext = 3;

    public static int Run(DiffRequest request, IJsonFileLoader loader, TextWriter output, TextWriter error)
    {
        DiffResult result;
        try
        {
            result = request.Compare.Diff(loader);
        }
        catch (Exception ex) when (CompareRequest.IsUserError(ex))
        {
            error.WriteLine(ex.Message);
            return ExitCodes.Error;
        }

        var hunks = DiffHunks.Build(result, request.Context);
        if (request.Json)
            WriteJson(output, request.Compare, result, hunks);
        else
            WriteUnified(output, request.Compare, result, hunks);

        return result.Summary.IsIdentical ? ExitCodes.Identical : ExitCodes.Different;
    }

    /// <summary>
    /// The unified diff format of <c>diff -u</c>, so any tool that reads patches can show it. A row whose sides have
    /// the same text is context; otherwise its left line is removed and its right line added, the removals of a run
    /// of such rows before its additions. An ignored row whose sides differ therefore shows as a change inside a
    /// hunk; only the JSON output marks it as ignored.
    /// </summary>
    private static void WriteUnified(
        TextWriter output, CompareRequest request, DiffResult result, IReadOnlyList<DiffHunk> hunks)
    {
        if (hunks.Count == 0)
            return;

        output.WriteLine($"--- {request.LeftPath}");
        output.WriteLine($"+++ {request.RightPath}");
        var removed = new List<string>();
        var added = new List<string>();
        foreach (var hunk in hunks)
        {
            output.WriteLine(
                $"@@ -{hunk.LeftStart},{hunk.LeftCount} +{hunk.RightStart},{hunk.RightCount} @@{SourceHeading(hunk)}");
            for (var row = hunk.StartRow; row <= hunk.EndRow; row++)
            {
                var (left, right) = (result.Left[row], result.Right[row]);
                if (left.DisplayLine is not null && right.DisplayLine is not null && left.Text == right.Text)
                {
                    Flush();
                    output.WriteLine($" {left.Text}");
                    continue;
                }
                if (left.DisplayLine is not null)
                    removed.Add(left.Text);
                if (right.DisplayLine is not null)
                    added.Add(right.Text);
            }
            Flush();
        }

        void Flush()
        {
            foreach (var line in removed)
                output.WriteLine($"-{line}");
            foreach (var line in added)
                output.WriteLine($"+{line}");
            removed.Clear();
            added.Clear();
        }
    }

    /// <summary>
    /// The section heading after a hunk's <c>@@ … @@</c>, where git puts the function name: the span of source lines
    /// each side's rows come from, e.g. <c> source -12..18 +15..21</c>. Patch tools ignore it. A side without source
    /// lines in the hunk shows as <c>-</c>; the heading is left out when neither side has any.
    /// </summary>
    private static string SourceHeading(DiffHunk hunk)
    {
        if (hunk.LeftSource is null && hunk.RightSource is null)
            return "";
        return $" source -{Span(hunk.LeftSource)} +{Span(hunk.RightSource)}";

        static string Span(LineSpan? span) => span is null ? "-" : $"{span.First}..{span.Last}";
    }

    private static void WriteJson(
        TextWriter output, CompareRequest request, DiffResult result, IReadOnlyList<DiffHunk> hunks)
    {
        var summary = result.Summary;
        var diff = new
        {
            left = request.LeftPath,
            right = request.RightPath,
            identical = summary.IsIdentical,
            summary = new
            {
                summary.Modified,
                summary.TypeChanged,
                summary.LeftOnly,
                summary.RightOnly,
                summary.Total,
            },
            hunks = hunks.Select(hunk => new
            {
                left = Side(hunk.LeftStart, hunk.LeftCount, hunk.LeftSource),
                right = Side(hunk.RightStart, hunk.RightCount, hunk.RightSource),
                rows = Enumerable.Range(hunk.StartRow, hunk.EndRow - hunk.StartRow + 1).Select(row => new
                {
                    left = Line(result.Left[row]),
                    right = Line(result.Right[row]),
                }),
            }),
        };
        output.WriteLine(JsonSerializer.Serialize(diff, JsonOutput.Options));
    }

    /// <summary>
    /// One side of a hunk: its display line range as in unified diff, and the lowest and highest source line of its
    /// rows (null when it has none).
    /// </summary>
    private static object Side(int start, int count, LineSpan? source) =>
        new { start, count, sourceFirst = source?.First, sourceLast = source?.Last };

    /// <summary>Null for a gap, which has no line.</summary>
    private static object? Line(DiffLine line) => line.DisplayLine is { } number
        ? new { kind = KindName(line.Kind), displayLine = number, sourceLine = line.SourceLine, text = line.Text }
        : null;

    private static string KindName(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Unchanged => "unchanged",
        DiffLineKind.Unmatched => "unmatched",
        DiffLineKind.Modified => "modified",
        DiffLineKind.TypeChanged => "typeChanged",
        DiffLineKind.Ignored => "ignored",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
