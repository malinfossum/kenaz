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

    [Test]
    public void SaveAll_then_LoadAll_preserves_decimal_precision()
    {
        var repository = new SqliteCheckInRepository(_filePath);
        var checkIn = new CheckIn(Day, mood: null, energy: null, sleep: 7.5m, note: null, createdAt: Created, updatedAt: Created);

        repository.SaveAll(new[] { checkIn });
        var loaded = repository.LoadAll();

        // The TEXT storage path preserves 7.5 exactly; REAL would risk 7.4999...
        Assert.That(loaded[0].Sleep, Is.EqualTo(7.5m));
    }

    [Test]
    public void SaveAll_then_LoadAll_preserves_null_fields()
    {
        var repository = new SqliteCheckInRepository(_filePath);
        // Only mood is set — every other optional field stays null (never 0, never "").
        var checkIn = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

        repository.SaveAll(new[] { checkIn });
        var loaded = repository.LoadAll();

        Assert.That(loaded[0].Mood, Is.EqualTo(5));
        Assert.That(loaded[0].Energy, Is.Null);
        Assert.That(loaded[0].Sleep, Is.Null);
        Assert.That(loaded[0].Note, Is.Null);
    }

    [Test]
    public void SaveAll_replaces_previous_state()
    {
        var repository = new SqliteCheckInRepository(_filePath);
        var first = new CheckIn(Day, mood: 7, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);
        var second = new CheckIn(Day.AddDays(1), mood: 3, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

        repository.SaveAll(new[] { first });
        repository.SaveAll(new[] { second });
        var loaded = repository.LoadAll();

        // The second SaveAll wins entirely — no leftover row from the first.
        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Date, Is.EqualTo(Day.AddDays(1)));
        Assert.That(loaded[0].Mood, Is.EqualTo(3));
    }

    [Test]
    public void SaveAll_empty_list_clears_the_table()
    {
        var repository = new SqliteCheckInRepository(_filePath);
        var checkIn = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

        repository.SaveAll(new[] { checkIn });
        repository.SaveAll(Array.Empty<CheckIn>());

        Assert.That(repository.LoadAll(), Is.Empty);
    }

    [Test]
    public void SaveAll_with_duplicate_dates_in_input_rolls_back_atomically()
    {
        var repository = new SqliteCheckInRepository(_filePath);
        var seed = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);
        repository.SaveAll(new[] { seed });

        var dup1 = new CheckIn(Day.AddDays(1), mood: 1, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);
        var dup2 = new CheckIn(Day.AddDays(1), mood: 2, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => repository.SaveAll(new[] { dup1, dup2 }));

        // The transaction rolled back — the seed row from the previous SaveAll is still there,
        // and no partial state from the duplicate-dates input is visible.
        var loaded = repository.LoadAll();
        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Date, Is.EqualTo(Day));
        Assert.That(loaded[0].Mood, Is.EqualTo(5));
    }

    [Test]
    public void Constructor_on_corrupt_db_backs_it_up_and_starts_fresh()
    {
        // Plant a "DB" that's actually random bytes — opening this as SQLite throws.
        File.WriteAllBytes(_filePath, new byte[] { 0x00, 0x42, 0x69, 0x6e, 0xff, 0x00 });

        var repository = new SqliteCheckInRepository(_filePath);

        // The bad file got renamed away…
        var corruptBackups = Directory.GetFiles(_dir, "checkins.db.corrupt-*.bak");
        Assert.That(corruptBackups, Has.Length.EqualTo(1));
        // …and a fresh, empty SQLite database took its place.
        Assert.That(File.Exists(_filePath), Is.True);
        Assert.That(repository.LoadAll(), Is.Empty);
    }
}
