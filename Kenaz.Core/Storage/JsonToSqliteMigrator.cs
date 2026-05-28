using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Kenaz.Core;

/// <summary>
/// One-shot migration from the legacy JSON store to the SQLite store. Idempotent —
/// runs every startup but only does work when the live DB doesn't yet exist (or when
/// a previous run left an interrupted state to clean up). Uses a sentinel path
/// (<c>dbPath + ".migrating"</c>) and atomic <c>File.Move</c> promotion so a process
/// kill during migration cannot leave the user with an empty live store while the
/// JSON still has data. The verified-readback gate (hardening invariant #2) and the
/// importable export-format backup are part of the same flow.
/// </summary>
public static class JsonToSqliteMigrator
{
    public static MigrationOutcome MigrateIfNeeded(string jsonPath, string dbPath, DateTimeOffset now)
    {
        return MigrateIfNeeded(jsonPath, dbPath, now, DefaultVerifier);
    }

    internal static MigrationOutcome MigrateIfNeeded(
        string jsonPath,
        string dbPath,
        DateTimeOffset now,
        Func<IReadOnlyList<CheckIn>, IReadOnlyList<CheckIn>, bool> verify)
    {
        // Pre-step: clear any orphan .migrating file left from a previous interrupted run.
        var migratingPath = dbPath + ".migrating";
        if (File.Exists(migratingPath))
        {
            File.Delete(migratingPath);
        }

        // Pre-step: if a previous migration crashed between promoting the new DB and writing
        // the backup, both files coexist. Finish the cleanup now and short-circuit to CleanedUp
        // (do NOT fall through to rule 1, which would return NoOp and skip the orientation line).
        if (File.Exists(dbPath) && File.Exists(jsonPath))
        {
            var legacyRecords = new JsonCheckInRepository(jsonPath).LoadAll();
            var cleanupBackupPath = Path.Combine(
                Path.GetDirectoryName(dbPath)!,
                $"checkins.backup-{now:yyyyMMdd-HHmmss}.json");
            new JsonCheckInArchive().Export(cleanupBackupPath, legacyRecords, now);
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
            return MigrationOutcome.CleanedUp;
        }

        if (File.Exists(dbPath))
        {
            return MigrationOutcome.NoOp;
        }

        if (!File.Exists(jsonPath))
        {
            _ = new SqliteCheckInRepository(dbPath);
            return MigrationOutcome.FreshInstall;
        }

        var source = new JsonCheckInRepository(jsonPath).LoadAll();

        try
        {
            var repo = new SqliteCheckInRepository(migratingPath);
            repo.SaveAll(source);

            var verifyList = repo.LoadAll();
            var sortedSource = source.OrderBy(c => c.Date).ToList();
            var sortedVerify = verifyList.OrderBy(c => c.Date).ToList();
            if (!verify(sortedSource, sortedVerify))
            {
                // Microsoft.Data.Sqlite pools connections, so the SaveAll/LoadAll above can
                // leave the migrating file's handle in the pool on Windows — clear pools so
                // File.Delete doesn't hit a sharing violation. Same defensive idiom as
                // SqliteCheckInRepository.BackUpCorruptFile.
                SqliteConnection.ClearAllPools();
                if (File.Exists(migratingPath))
                {
                    File.Delete(migratingPath);
                }
                throw new MigrationException("Verification failed — the new database didn't match the source.");
            }

            // Same pool reason as above: File.Move on Windows fails with IOException if
            // the SQLite connection pool still holds the migrating file's handle.
            SqliteConnection.ClearAllPools();
            File.Move(migratingPath, dbPath);
        }
        catch (SqliteException ex)
        {
            // Same pool reason as inside the try: clear pools before File.Delete to avoid
            // a sharing violation if the connection handle is still pooled on Windows.
            SqliteConnection.ClearAllPools();
            if (File.Exists(migratingPath))
            {
                File.Delete(migratingPath);
            }
            throw new MigrationException("Couldn't write the new database — check disk space and try again.", ex);
        }
        catch (IOException ex)
        {
            // Same pool reason as inside the try: clear pools before File.Delete to avoid
            // a sharing violation if the connection handle is still pooled on Windows.
            SqliteConnection.ClearAllPools();
            if (File.Exists(migratingPath))
            {
                File.Delete(migratingPath);
            }
            throw new MigrationException("Couldn't promote the new database — check filesystem and try again.", ex);
        }

        var backupPath = Path.Combine(
            Path.GetDirectoryName(dbPath)!,
            $"checkins.backup-{now:yyyyMMdd-HHmmss}.json");
        new JsonCheckInArchive().Export(backupPath, source, now);
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
        }

        return MigrationOutcome.Migrated;
    }

    private static bool DefaultVerifier(IReadOnlyList<CheckIn> source, IReadOnlyList<CheckIn> readback)
    {
        if (source.Count != readback.Count)
        {
            return false;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var a = source[i];
            var b = readback[i];
            if (a.Date != b.Date) return false;
            if (a.Mood != b.Mood) return false;
            if (a.Energy != b.Energy) return false;
            if (a.Sleep != b.Sleep) return false;
            if (a.Note != b.Note) return false;
            if (a.CreatedAt != b.CreatedAt) return false;
            if (a.UpdatedAt != b.UpdatedAt) return false;
        }

        return true;
    }
}
