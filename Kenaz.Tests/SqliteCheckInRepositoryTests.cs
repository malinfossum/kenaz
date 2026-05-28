using Kenaz.Core;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class SqliteCheckInRepositoryTests
{
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
}
