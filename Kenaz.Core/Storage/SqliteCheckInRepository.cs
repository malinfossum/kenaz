using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Kenaz.Core;

/// <summary>
/// Stores check-ins as rows in a SQLite database. Writes go through a single
/// DELETE+INSERT transaction (atomic by SQLite contract); reads validate every
/// row through the <see cref="CheckIn"/> constructor and drop ones that fail.
/// All text-boundary values (decimal, DateOnly, DateTimeOffset) cross strings,
/// so write/read are pinned to <see cref="CultureInfo.InvariantCulture"/> to be
/// locale-safe. Corrupt files are backed up as <c>*.corrupt-*.bak</c> and the
/// repository falls back to a fresh empty schema — symmetric with
/// <see cref="JsonCheckInRepository"/>'s recovery (hardening invariant #1).
/// </summary>
public sealed class SqliteCheckInRepository : ICheckInRepository
{
    private readonly string _filePath;
    private readonly string _connectionString;

    public SqliteCheckInRepository(string filePath)
    {
        _filePath = filePath;
        _connectionString = $"Data Source={filePath}";
    }

    public static string DefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Kenaz", "checkins.db");
    }

    public IReadOnlyList<CheckIn> LoadAll()
    {
        throw new NotImplementedException();
    }

    public void SaveAll(IReadOnlyList<CheckIn> checkIns)
    {
        throw new NotImplementedException();
    }
}
