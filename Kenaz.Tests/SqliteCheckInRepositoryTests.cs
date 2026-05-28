using Kenaz.Core;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class SqliteCheckInRepositoryTests
{
    private static readonly DateOnly Day = new DateOnly(2026, 5, 22);
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset Updated = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.FromHours(2));

    private string _dir = null!;
    private string _filePath = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _filePath = Path.Combine(_dir, "checkins.db");
    }

    [TearDown]
    public void TearDown()
    {
        // Microsoft.Data.Sqlite pools connections by default; on Windows the pool
        // keeps the file handle alive after `using` blocks dispose, so the temp
        // folder can't be deleted until the pool is released.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public void DefaultFilePath_ends_with_Kenaz_checkins_db()
    {
        var path = SqliteCheckInRepository.DefaultFilePath();

        Assert.That(path, Does.EndWith(Path.Combine("Kenaz", "checkins.db")));
    }

    [Test]
    public void Schema_is_created_on_first_open()
    {
        // Just constructing the repository should leave a queryable database behind it.
        _ = new SqliteCheckInRepository(_filePath);

        Assert.That(File.Exists(_filePath), Is.True);
        // And opening a new repository over the same file should not throw.
        Assert.DoesNotThrow(() => _ = new SqliteCheckInRepository(_filePath));
    }

    [Test]
    public void LoadAll_returns_empty_when_db_is_new()
    {
        var repository = new SqliteCheckInRepository(_filePath);

        Assert.That(repository.LoadAll(), Is.Empty);
    }

    [Test]
    public void SaveAll_then_LoadAll_round_trips_all_fields_including_timestamps()
    {
        var repository = new SqliteCheckInRepository(_filePath);
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
}
