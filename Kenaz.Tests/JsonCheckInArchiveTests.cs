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

    [Test]
    public void Export_then_import_round_trips_with_timestamps()
    {
        var checkIn = new CheckIn(Day, mood: 7, energy: 6, sleep: 7.5m, note: "ok day", createdAt: Created, updatedAt: Updated);
        var archive = new JsonCheckInArchive();

        archive.Export(_path, new[] { checkIn }, exportedAt: Updated);
        var result = archive.Import(_path);

        Assert.That(result.Records, Has.Count.EqualTo(1));
        Assert.That(result.Skipped, Is.EqualTo(0));
        var loaded = result.Records[0];
        Assert.That(loaded.Date, Is.EqualTo(Day));
        Assert.That(loaded.Mood, Is.EqualTo(7));
        Assert.That(loaded.CreatedAt, Is.EqualTo(Created));
        Assert.That(loaded.UpdatedAt, Is.EqualTo(Updated));
    }

    [Test]
    public void Import_drops_records_that_break_invariants_and_counts_them()
    {
        var json = """
        {
          "SchemaVersion": 1,
          "ExportedAt": "2026-05-22T20:00:00+00:00",
          "CheckIns": [
            { "Date": "2026-05-22", "Mood": 7, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-22T08:00:00+00:00", "UpdatedAt": "2026-05-22T20:00:00+00:00" },
            { "Date": "2026-05-21", "Mood": 99, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-21T08:00:00+00:00", "UpdatedAt": "2026-05-21T20:00:00+00:00" }
          ]
        }
        """;
        File.WriteAllText(_path, json);

        var result = new JsonCheckInArchive().Import(_path);

        Assert.That(result.Records, Has.Count.EqualTo(1));
        Assert.That(result.Skipped, Is.EqualTo(1));
    }

    [Test]
    public void Import_envelope_with_no_records_returns_empty()
    {
        var json = """
        { "SchemaVersion": 1, "ExportedAt": "2026-05-22T20:00:00+00:00", "CheckIns": null }
        """;
        File.WriteAllText(_path, json);

        var result = new JsonCheckInArchive().Import(_path);

        Assert.That(result.Records, Is.Empty);
        Assert.That(result.Skipped, Is.EqualTo(0));
    }

    [Test]
    public void Import_missing_file_throws_ImportException()
    {
        Assert.Throws<ImportException>(() => new JsonCheckInArchive().Import(_path));
    }

    [Test]
    public void Import_unreadable_file_throws_ImportException()
    {
        File.WriteAllText(_path, "this is not json");

        Assert.Throws<ImportException>(() => new JsonCheckInArchive().Import(_path));
    }
}
