using Kenaz.Core;

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
}
