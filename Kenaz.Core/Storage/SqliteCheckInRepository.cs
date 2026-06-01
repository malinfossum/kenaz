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
        try
        {
            return LoadAllCore();
        }
        catch (SqliteException)
        {
            BackUpCorruptFile();
            EnsureSchema();
            return new List<CheckIn>();
        }
    }

    /// <summary>
    /// Opens a connection and sets busy_timeout so a writer that arrives mid-transaction waits up
    /// to 3 s for the lock instead of throwing SQLITE_BUSY. Internal only so the busy_timeout test
    /// can read the PRAGMA back; not part of the public surface.
    /// </summary>
    internal SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout = 3000;";
            pragma.ExecuteNonQuery();
        }

        return conn;
    }

    private IReadOnlyList<CheckIn> LoadAllCore()
    {
        using var conn = OpenConnection();

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
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        using (var clearCmd = conn.CreateCommand())
        {
            clearCmd.Transaction = tx;
            clearCmd.CommandText = "DELETE FROM CheckIns";
            clearCmd.ExecuteNonQuery();
        }

        if (checkIns.Count > 0)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText = """
                INSERT INTO CheckIns (Date, Mood, Energy, Sleep, Note, CreatedAt, UpdatedAt)
                VALUES ($date, $mood, $energy, $sleep, $note, $createdAt, $updatedAt)
                """;

            var pDate      = insertCmd.Parameters.Add("$date",      SqliteType.Text);
            var pMood      = insertCmd.Parameters.Add("$mood",      SqliteType.Integer);
            var pEnergy    = insertCmd.Parameters.Add("$energy",    SqliteType.Integer);
            var pSleep     = insertCmd.Parameters.Add("$sleep",     SqliteType.Text);
            var pNote      = insertCmd.Parameters.Add("$note",      SqliteType.Text);
            var pCreatedAt = insertCmd.Parameters.Add("$createdAt", SqliteType.Text);
            var pUpdatedAt = insertCmd.Parameters.Add("$updatedAt", SqliteType.Text);

            foreach (var c in checkIns)
            {
                pDate.Value      = c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                pMood.Value      = (object?)c.Mood ?? DBNull.Value;
                pEnergy.Value    = (object?)c.Energy ?? DBNull.Value;
                pSleep.Value     = c.Sleep.HasValue
                    ? c.Sleep.Value.ToString(CultureInfo.InvariantCulture)
                    : (object)DBNull.Value;
                pNote.Value      = (object?)c.Note ?? DBNull.Value;
                pCreatedAt.Value = c.CreatedAt.ToString("o", CultureInfo.InvariantCulture);
                pUpdatedAt.Value = c.UpdatedAt.ToString("o", CultureInfo.InvariantCulture);

                insertCmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    private void EnsureSchema()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            OpenAndRunSchema();
        }
        catch (SqliteException)
        {
            BackUpCorruptFile();
            OpenAndRunSchema();
        }
    }

    private void OpenAndRunSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = SchemaSql;
        cmd.ExecuteNonQuery();
    }

    private void BackUpCorruptFile()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        // Microsoft.Data.Sqlite pools connections, so the previous (failed) open
        // can still hold the file handle on Windows even after `using` disposed it.
        // Clear pools so File.Move can rename the doomed file.
        SqliteConnection.ClearAllPools();

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = _filePath + $".corrupt-{timestamp}.bak";
        File.Move(_filePath, backupPath, overwrite: true);
    }
}
