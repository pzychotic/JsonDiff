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

    [TestCase("stats", "missing.json", "missing.json")]
    [TestCase("stats")]
    [TestCase("stats", "-t", "1,5", "a.json", "b.json")]
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
