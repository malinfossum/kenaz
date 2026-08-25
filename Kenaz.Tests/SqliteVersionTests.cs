using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

/// <summary>
/// Guards the native SQLite the app actually loads, not the managed package version.
///
/// Kenaz.Core carried an explicit SQLitePCLRaw.bundle_e_sqlite3 pin for CVE-2025-6965 —
/// a high-severity memory-corruption bug in SQLite before 3.50.2 — because the
/// Microsoft.Data.Sqlite release of the day still pulled a vulnerable native lib. That pin
/// is gone now that Microsoft.Data.Sqlite ships a patched one itself.
///
/// This test is what replaces the pin. The managed dependency graph can move under us
/// without anyone noticing which SQLite binary comes along; asking the engine directly is
/// the only claim worth trusting. If this ever fails, do not lower the floor — restore the
/// bundle_e_sqlite3 pin at a version whose native lib is >= 3.50.2.
/// </summary>
public class SqliteVersionTests
{
    // CVE-2025-6965 is fixed in 3.50.2.
    private static readonly Version Minimum = new Version(3, 50, 2);

    [Test]
    public void BundledSqliteClearsTheCve20256965Floor()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select sqlite_version()";
        var reported = (string)command.ExecuteScalar()!;

        Assert.That(Version.TryParse(reported, out var actual), Is.True, $"unparseable sqlite_version(): {reported}");
        Assert.That(
            actual, Is.GreaterThanOrEqualTo(Minimum),
            $"native SQLite is {reported}, below the {Minimum} fix for CVE-2025-6965");
    }
}
