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
        if (File.Exists(dbPath))
        {
            return MigrationOutcome.NoOp;
        }

        throw new NotImplementedException("Other cases land in later tasks.");
    }

    private static bool DefaultVerifier(IReadOnlyList<CheckIn> source, IReadOnlyList<CheckIn> readback)
    {
        // Implemented properly when the verify path arrives. For now, fail closed if called.
        return false;
    }
}
