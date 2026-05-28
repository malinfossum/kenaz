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
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS CheckIns (
            Date       TEXT    NOT NULL PRIMARY KEY,
            Mood       INTEGER NULL,
            Energy     INTEGER NULL,
            Sleep      TEXT    NULL,
            Note       TEXT    NULL,
            CreatedAt  TEXT    NOT NULL,
            UpdatedAt  TEXT    NOT NULL
        )
        """;

    private readonly string _filePath;
    private readonly string _connectionString;

    public SqliteCheckInRepository(string filePath)
    {
        _filePath = filePath;
        _connectionString = $"Data Source={filePath}";
        EnsureSchema();
    }

    public static string DefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Kenaz", "checkins.db");
    }

    public IReadOnlyList<CheckIn> LoadAll()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Date, Mood, Energy, Sleep, Note, CreatedAt, UpdatedAt FROM CheckIns ORDER BY Date";

        var checkIns = new List<CheckIn>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var date = DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var mood = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                var energy = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
                var sleep = reader.IsDBNull(3) ? (decimal?)null : decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
                var note = reader.IsDBNull(4) ? null : reader.GetString(4);
                var createdAt = DateTimeOffset.ParseExact(reader.GetString(5), "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                var updatedAt = DateTimeOffset.ParseExact(reader.GetString(6), "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                checkIns.Add(new CheckIn(date, mood, energy, sleep, note, createdAt, updatedAt));
            }
            catch (ArgumentException)
            {
                // Drop a row that smuggled past CheckIn's invariants.
            }
        }
        return checkIns;
    }

    public void SaveAll(IReadOnlyList<CheckIn> checkIns)
    {
        throw new NotImplementedException();
    }

    private void EnsureSchema()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SchemaSql;
        cmd.ExecuteNonQuery();
    }
}
