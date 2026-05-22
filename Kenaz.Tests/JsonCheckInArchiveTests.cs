using Kenaz.Core;

namespace Kenaz.Tests;

public class JsonCheckInArchiveTests
{
    private static readonly DateOnly Day = new DateOnly(2026, 5, 22);
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.Zero);

    private string _dir = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-archive-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "export.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public void Export_writes_a_versioned_envelope()
    {
        var checkIn = new CheckIn(Day, mood: 7, energy: 6, sleep: 7.5m, note: "ok day", createdAt: Created, updatedAt: Updated);

        new JsonCheckInArchive().Export(_path, new[] { checkIn }, exportedAt: Updated);

        Assert.That(File.Exists(_path), Is.True);
        var json = File.ReadAllText(_path);
        Assert.That(json, Does.Contain("\"SchemaVersion\": 1"));
        Assert.That(json, Does.Contain("\"Mood\": 7"));
    }
}
