using Kenaz.Core;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class JsonToSqliteMigratorTests
{
    private string _dir = null!;
    private string _jsonPath = null!;
    private string _dbPath = null!;
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.FromHours(2));
    private static readonly DateOnly Day1 = new DateOnly(2026, 5, 25);
    private static readonly DateOnly Day2 = new DateOnly(2026, 5, 26);

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _jsonPath = Path.Combine(_dir, "checkins.json");
        _dbPath = Path.Combine(_dir, "checkins.db");
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public void Skips_when_db_already_exists_returns_NoOp()
    {
        // Seed a real (empty) DB so the migrator sees dbPath exists.
        _ = new SqliteCheckInRepository(_dbPath);

        var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        Assert.That(outcome, Is.EqualTo(MigrationOutcome.NoOp));
        Assert.That(File.Exists(_dbPath), Is.True);
        Assert.That(File.Exists(_jsonPath), Is.False);
    }

    [Test]
    public void Fresh_install_creates_empty_db_and_no_backup_returns_FreshInstall()
    {
        var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        Assert.That(outcome, Is.EqualTo(MigrationOutcome.FreshInstall));
        Assert.That(File.Exists(_dbPath), Is.True);
        Assert.That(new SqliteCheckInRepository(_dbPath).LoadAll(), Is.Empty);
        Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Is.Empty);
    }

    private void WriteLegacyJson(params CheckIn[] records)
    {
        // Reuse the existing JSON repo to write a legacy file in the format the migrator will read.
        new JsonCheckInRepository(_jsonPath).SaveAll(records);
    }

    private static CheckIn MakeCheckIn(DateOnly date, int? mood = 5, decimal? sleep = 7.0m)
    {
        var ts = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(2));
        return new CheckIn(date, mood, energy: null, sleep: sleep, note: null, createdAt: ts, updatedAt: ts);
    }

    [Test]
    public void Migrates_records_from_legacy_json_into_db_returns_Migrated()
    {
        WriteLegacyJson(MakeCheckIn(Day1, mood: 7), MakeCheckIn(Day2, mood: 4));

        var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        Assert.That(outcome, Is.EqualTo(MigrationOutcome.Migrated));
        var loaded = new SqliteCheckInRepository(_dbPath).LoadAll();
        Assert.That(loaded.Count, Is.EqualTo(2));
        Assert.That(loaded.Select(c => c.Mood), Is.EquivalentTo(new int?[] { 7, 4 }));
    }

    [Test]
    public void Writes_export_format_backup_after_successful_migration()
    {
        WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

        JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        var backups = Directory.GetFiles(_dir, "checkins.backup-*.json");
        Assert.That(backups, Has.Length.EqualTo(1));
        // The file should be in the envelope format, i.e. readable by JsonCheckInArchive.Import.
        var imported = new JsonCheckInArchive().Import(backups[0]);
        Assert.That(imported.Records.Count, Is.EqualTo(1));
        Assert.That(imported.Records[0].Mood, Is.EqualTo(7));
    }

    [Test]
    public void Deletes_original_json_after_successful_migration()
    {
        WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

        JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        Assert.That(File.Exists(_jsonPath), Is.False);
    }

    [Test]
    public void Backup_is_importable_via_archive()
    {
        WriteLegacyJson(MakeCheckIn(Day1, mood: 7), MakeCheckIn(Day2, mood: 4));

        JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        var backupPath = Directory.GetFiles(_dir, "checkins.backup-*.json").Single();
        var imported = new JsonCheckInArchive().Import(backupPath);

        // The backup is round-trippable through the existing import path — proof of recoverability.
        Assert.That(imported.Records, Has.Count.EqualTo(2));
        Assert.That(imported.Records.Select(c => c.Date), Is.EquivalentTo(new[] { Day1, Day2 }));
    }

    [Test]
    public void Migrated_db_round_trips_with_journal()
    {
        WriteLegacyJson(MakeCheckIn(Day1, mood: 7), MakeCheckIn(Day2, mood: 4));

        JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        var repo = new SqliteCheckInRepository(_dbPath);
        var journal = new WellbeingJournal(repo, () => Now);
        var history = journal.History();
        Assert.That(history, Has.Count.EqualTo(2));
        Assert.That(history.Select(c => c.Mood), Is.EquivalentTo(new int?[] { 7, 4 }));
    }

    [Test]
    public void Verification_failure_deletes_migrating_path_and_keeps_json()
    {
        WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

        Assert.Throws<MigrationException>(() =>
            JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now, verify: (a, b) => false));

        Assert.That(File.Exists(_dbPath), Is.False, "Live store should never have been promoted.");
        Assert.That(File.Exists(_dbPath + ".migrating"), Is.False, "Sentinel path should be cleaned up.");
        Assert.That(File.Exists(_jsonPath), Is.True, "Source JSON must be untouched on failure.");
        Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Is.Empty, "Backup must not be written.");
    }

    [Test]
    public void Corrupt_legacy_json_results_in_empty_db_and_corrupt_backup()
    {
        File.WriteAllText(_jsonPath, "{ not valid json ");

        var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

        Assert.That(outcome, Is.EqualTo(MigrationOutcome.Migrated));
        Assert.That(File.Exists(_dbPath), Is.True);
        Assert.That(new SqliteCheckInRepository(_dbPath).LoadAll(), Is.Empty);

        // The corrupt JSON was renamed by JsonCheckInRepository's recovery before the migrator saw it…
        var corruptBackups = Directory.GetFiles(_dir, "checkins.json.corrupt-*.bak");
        Assert.That(corruptBackups, Has.Length.EqualTo(1));
        // …and the (empty) export-format backup is also present.
        Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Has.Length.EqualTo(1));
    }
}
