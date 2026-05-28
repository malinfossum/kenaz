using Kenaz.Core;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class JsonToSqliteMigratorTests
{
    private string _dir = null!;
    private string _jsonPath = null!;
    private string _dbPath = null!;
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.FromHours(2));

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
}
