using System.IO;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Tests.Services;

[TestFixture]
public class JsonSettingsServiceTests
{
    private string _directory = "";
    private string _path = "";

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"JsonDiffTests_{Guid.NewGuid():N}");
        _path = Path.Combine(_directory, "Settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void SaveThenLoad_RoundTripsAllProperties()
    {
        var service = new JsonSettingsService(_directory);
        var settings = new AppSettings
        {
            WindowLeft = 10.5,
            WindowTop = -20,
            WindowWidth = 1024,
            WindowHeight = 768,
            WindowMaximized = true,
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.WindowLeft, Is.EqualTo(10.5));
            Assert.That(loaded.WindowTop, Is.EqualTo(-20));
            Assert.That(loaded.WindowWidth, Is.EqualTo(1024));
            Assert.That(loaded.WindowHeight, Is.EqualTo(768));
            Assert.That(loaded.WindowMaximized, Is.True);
        });
    }

    [Test]
    public void SaveThenLoad_RoundTripsDefaultValues()
    {
        var service = new JsonSettingsService(_directory);

        service.Save(new AppSettings());
        var loaded = service.Load();

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.WindowLeft, Is.Null);
            Assert.That(loaded.WindowTop, Is.Null);
            Assert.That(loaded.WindowWidth, Is.Null);
            Assert.That(loaded.WindowHeight, Is.Null);
            Assert.That(loaded.WindowMaximized, Is.False);
        });
    }

    [Test]
    public void Save_OverwritesPreviousSettings()
    {
        var service = new JsonSettingsService(_directory);

        service.Save(new AppSettings { WindowWidth = 640 });
        service.Save(new AppSettings { WindowWidth = 1920 });

        Assert.That(service.Load()?.WindowWidth, Is.EqualTo(1920));
    }

    [Test]
    public void Save_CreatesTheMissingDirectory()
    {
        var nested = Path.Combine(_directory, "sub", "folder");
        var service = new JsonSettingsService(nested);

        service.Save(new AppSettings { WindowWidth = 500 });

        Assert.That(File.Exists(Path.Combine(nested, "Settings.json")), Is.True);
        Assert.That(service.Load()?.WindowWidth, Is.EqualTo(500));
    }

    [Test]
    public void Save_DoesNotThrowWhenTheFileCannotBeWritten()
    {
        // A directory where the file should be makes the write fail.
        Directory.CreateDirectory(_path);

        var service = new JsonSettingsService(_directory);

        Assert.That(() => service.Save(new AppSettings { WindowWidth = 1024 }), Throws.Nothing);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileIsMissing()
    {
        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileIsMalformed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not valid json !");

        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileContainsJsonNull()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "null");

        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }

    [Test]
    public void Load_ReturnsNullWhenTheFileIsLocked()
    {
        Directory.CreateDirectory(_directory);
        using var stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write([1, 2, 3]);

        var service = new JsonSettingsService(_directory);

        Assert.That(service.Load(), Is.Null);
    }

    [Test]
    public void Load_LeavesMissingPropertiesAtTheirDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """{ "WindowWidth": 800 }""");

        var service = new JsonSettingsService(_directory);
        var loaded = service.Load();

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.WindowWidth, Is.EqualTo(800));
            Assert.That(loaded.WindowLeft, Is.Null);
            Assert.That(loaded.WindowMaximized, Is.False);
        });
    }
}
