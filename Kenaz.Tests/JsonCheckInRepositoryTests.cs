using Kenaz.Core;

namespace Kenaz.Tests;

public class JsonCheckInRepositoryTests
{
    private static readonly DateOnly Day = new DateOnly(2026, 5, 22);
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.Zero);

    private string _dir = null!;
    private string _filePath = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _filePath = Path.Combine(_dir, "checkins.json");
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
    public void LoadAll_returns_empty_when_file_is_missing()
    {
        var repository = new JsonCheckInRepository(_filePath);

        Assert.That(repository.LoadAll(), Is.Empty);
    }

    [Test]
    public void SaveAll_then_LoadAll_round_trips_all_fields_including_timestamps()
    {
        var repository = new JsonCheckInRepository(_filePath);
        var checkIn = new CheckIn(Day, mood: 7, energy: 6, sleep: 7.5m, note: "ok", createdAt: Created, updatedAt: Updated);

        repository.SaveAll(new[] { checkIn });
        var loaded = repository.LoadAll();

        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Date, Is.EqualTo(Day));
        Assert.That(loaded[0].Mood, Is.EqualTo(7));
        Assert.That(loaded[0].Energy, Is.EqualTo(6));
        Assert.That(loaded[0].Sleep, Is.EqualTo(7.5m));
        Assert.That(loaded[0].Note, Is.EqualTo("ok"));
        Assert.That(loaded[0].CreatedAt, Is.EqualTo(Created));
        Assert.That(loaded[0].UpdatedAt, Is.EqualTo(Updated));
    }

    [Test]
    public void SaveAll_writes_the_real_file_and_leaves_no_temp_file()
    {
        var repository = new JsonCheckInRepository(_filePath);
        var checkIn = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

        repository.SaveAll(new[] { checkIn });

        Assert.That(File.Exists(_filePath), Is.True);
        Assert.That(File.Exists(_filePath + ".tmp"), Is.False);
    }

    [Test]
    public void LoadAll_on_syntactically_corrupt_file_backs_it_up_and_returns_empty()
    {
        File.WriteAllText(_filePath, "{ this is not valid json ");
        var repository = new JsonCheckInRepository(_filePath);

        var loaded = repository.LoadAll();

        Assert.That(loaded, Is.Empty);
        Assert.That(File.Exists(_filePath), Is.False);
        Assert.That(Directory.GetFiles(_dir, "*corrupt*"), Has.Length.EqualTo(1));
    }

    [Test]
    public void LoadAll_drops_semantically_invalid_records()
    {
        var json =
            """
            [
              { "Date": "2026-05-22", "Mood": 7, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-22T08:00:00+00:00", "UpdatedAt": "2026-05-22T08:00:00+00:00" },
              { "Date": "2026-05-21", "Mood": 15, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-21T08:00:00+00:00", "UpdatedAt": "2026-05-21T08:00:00+00:00" },
              { "Date": "2026-05-20", "Mood": null, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-20T08:00:00+00:00", "UpdatedAt": "2026-05-20T08:00:00+00:00" }
            ]
            """;
        File.WriteAllText(_filePath, json);
        var repository = new JsonCheckInRepository(_filePath);

        var loaded = repository.LoadAll();

        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Date, Is.EqualTo(new DateOnly(2026, 5, 22)));
        Assert.That(loaded[0].Mood, Is.EqualTo(7));
    }

    [Test]
    public void LoadAll_keeps_only_one_record_per_date()
    {
        var json =
            """
            [
              { "Date": "2026-05-22", "Mood": 7, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-22T08:00:00+00:00", "UpdatedAt": "2026-05-22T08:00:00+00:00" },
              { "Date": "2026-05-22", "Mood": 3, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-22T09:00:00+00:00", "UpdatedAt": "2026-05-22T09:00:00+00:00" }
            ]
            """;
        File.WriteAllText(_filePath, json);
        var repository = new JsonCheckInRepository(_filePath);

        var loaded = repository.LoadAll();

        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Mood, Is.EqualTo(7));
    }

    [Test]
    public void DefaultFilePath_ends_with_Kenaz_checkins_json()
    {
        var path = JsonCheckInRepository.DefaultFilePath();

        Assert.That(path, Does.EndWith(Path.Combine("Kenaz", "checkins.json")));
    }
}
