using System.Text.Json.Nodes;
using JsonDiff.Cli;

namespace JsonDiff.Tests.Cli;

public class CliAppTests
{
    private static string Sample(string name) => Path.Combine(TestContext.CurrentContext.TestDirectory, "Samples", name);

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CliApp.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    [Test]
    public void Stats_DifferentFiles_PrintsCountsAndExitsWithOne()
    {
        var (exitCode, output, error) = Run("stats", Sample("items-left.json"), Sample("items-right.json"), "--no-rules");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Different));
            Assert.That(output, Does.Contain("Total").And.Contain("differences:"));
            Assert.That(error, Is.Empty);
        });
    }

    [Test]
    public void Stats_SameFile_ExitsWithZero()
    {
        var (exitCode, output, _) = Run("stats", Sample("items-left.json"), Sample("items-left.json"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Identical));
            Assert.That(output, Does.Contain("No differences."));
        });
    }

    [Test]
    public void Stats_Json_WritesSummaryAsJson()
    {
        var (exitCode, output, _) = Run("stats", Sample("items-left.json"), Sample("items-right.json"), "--json");

        var stats = JsonNode.Parse(output)!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Different));
            Assert.That(stats["identical"]!.GetValue<bool>(), Is.False);
            Assert.That(stats["total"]!.GetValue<int>(),
                Is.EqualTo(stats["modified"]!.GetValue<int>() + stats["typeChanged"]!.GetValue<int>() +
                           stats["leftOnly"]!.GetValue<int>() + stats["rightOnly"]!.GetValue<int>()));
        });
    }

    [Test]
    public void Stats_CompareNumbers_UsesTolerance()
    {
        string[] files = ["stats", Sample("numbers-left.json"), Sample("numbers-right.json"), "--no-rules"];

        Assert.Multiple(() =>
        {
            Assert.That(Run(files).ExitCode, Is.EqualTo(ExitCodes.Different));
            Assert.That(Run([.. files, "-n", "-t", "1000"]).ExitCode, Is.EqualTo(ExitCodes.Identical));
        });
    }

    [Test]
    public void Diff_DifferentFiles_WritesUnifiedDiffAndExitsWithOne()
    {
        var (exitCode, output, error) = Run("diff", Sample("left.json"), Sample("right.json"), "--no-rules");
        var lines = output.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Different));
            Assert.That(error, Is.Empty);
            Assert.That(lines[0], Is.EqualTo($"--- {Sample("left.json")}"));
            Assert.That(lines[1], Is.EqualTo($"+++ {Sample("right.json")}"));
            Assert.That(lines[2], Does.StartWith("@@ -"));
            Assert.That(lines, Does.Contain("-  \"version\": \"1.0.0\"").And.Contain("+  \"version\": \"1.1.0\""));
        });
        AssertHunkCountsMatch(lines);
    }

    [TestCase("0")]
    [TestCase("1")]
    [TestCase("10")]
    public void Diff_HunkHeadersMatchTheirLines(string context)
    {
        var (_, output, _) = Run("diff", Sample("items-left.json"), Sample("items-right.json"), "-U", context);

        AssertHunkCountsMatch(output.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'));
    }

    [Test]
    public void Diff_SameFile_WritesNothingAndExitsWithZero()
    {
        var (exitCode, output, error) = Run("diff", Sample("left.json"), Sample("left.json"));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Identical));
            Assert.That(output, Is.Empty);
            Assert.That(error, Is.Empty);
        });
    }

    [Test]
    public void Diff_Json_WritesHunksWithAlignedRows()
    {
        var (exitCode, output, _) = Run("diff", Sample("left.json"), Sample("right.json"), "--no-rules", "--json");

        var diff = JsonNode.Parse(output)!.AsObject();
        var hunks = diff["hunks"]!.AsArray();
        var rows = hunks.SelectMany(h => h!["rows"]!.AsArray()).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Different));
            Assert.That(diff["identical"]!.GetValue<bool>(), Is.False);
            Assert.That(diff["summary"]!["total"]!.GetValue<int>(), Is.GreaterThan(0));
            Assert.That(hunks, Is.Not.Empty);
            Assert.That(rows, Has.Some.Matches<JsonNode?>(r =>
                r!["left"]?["kind"]?.GetValue<string>() == "modified" &&
                r["left"]!["text"]!.GetValue<string>() == "  \"version\": \"1.0.0\"" &&
                r["right"]!["text"]!.GetValue<string>() == "  \"version\": \"1.1.0\""));
            // A row only on one side has null on the other.
            Assert.That(rows, Has.Some.Matches<JsonNode?>(r => r!["left"] is null && r["right"] is not null));
            foreach (var hunk in hunks)
            {
                var hunkRows = hunk!["rows"]!.AsArray();
                Assert.That(hunkRows.Count(r => r!["left"] is not null), Is.EqualTo(hunk["left"]!["count"]!.GetValue<int>()));
                Assert.That(hunkRows.Count(r => r!["right"] is not null), Is.EqualTo(hunk["right"]!["count"]!.GetValue<int>()));
            }
        });
    }

    [Test]
    public void Diff_Json_SameFile_HasNoHunks()
    {
        var (exitCode, output, _) = Run("diff", Sample("left.json"), Sample("left.json"), "--json");

        var diff = JsonNode.Parse(output)!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Identical));
            Assert.That(diff["identical"]!.GetValue<bool>(), Is.True);
            Assert.That(diff["hunks"]!.AsArray(), Is.Empty);
        });
    }

    [Test]
    public void Diff_NegativeContext_IsAnError()
    {
        var (exitCode, output, error) = Run("diff", Sample("left.json"), Sample("right.json"), "-U", "-1");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Error));
            Assert.That(output, Is.Empty);
            Assert.That(error, Does.Contain("--context"));
        });
    }

    /// <summary>Checks that each "@@ -a,b +c,d @@" header's counts match the lines of its hunk.</summary>
    private static void AssertHunkCountsMatch(string[] lines)
    {
        var headers = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"^@@ -\d+,(\d+) \+\d+,(\d+) @@$");
            if (!match.Success)
                continue;

            headers++;
            var body = lines.Skip(i + 1).TakeWhile(l => !l.StartsWith("@@")).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(body.Count(l => l[0] is ' ' or '-'), Is.EqualTo(int.Parse(match.Groups[1].Value)), lines[i]);
                Assert.That(body.Count(l => l[0] is ' ' or '+'), Is.EqualTo(int.Parse(match.Groups[2].Value)), lines[i]);
            });
        }
        Assert.That(headers, Is.GreaterThan(0));
    }

    [TestCase("stats", "missing.json", "missing.json")]
    [TestCase("stats")]
    [TestCase("stats", "-t", "1,5", "a.json", "b.json")]
    [TestCase("diff", "missing.json", "missing.json")]
    [TestCase("bogus")]
    public void InvalidArguments_ExitWithTwo(params string[] args)
    {
        var (exitCode, output, error) = Run(args);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Error));
            Assert.That(output, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void Stats_RulesAndNoRules_IsAnError()
    {
        var rules = Path.Combine(TestContext.CurrentContext.TestDirectory, "MatchRules.json");

        var (exitCode, _, error) = Run(
            "stats", Sample("items-left.json"), Sample("items-right.json"), "--rules", rules, "--no-rules");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Error));
            Assert.That(error, Does.Contain("--no-rules"));
        });
    }

    [Test]
    public void Stats_InvalidJson_ExitsWithTwoAndNamesTheFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ not json");

            var (exitCode, _, error) = Run("stats", path, Sample("items-left.json"));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ExitCodes.Error));
                Assert.That(error, Does.Contain(path));
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Help_ExitsWithZero()
    {
        var (exitCode, output, _) = Run("stats", "--help");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(ExitCodes.Identical));
            Assert.That(output, Does.Contain("--compare-numbers"));
        });
    }
}
