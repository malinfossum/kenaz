namespace Kenaz.Core;

/// <summary>
/// What <see cref="JsonToSqliteMigrator.MigrateIfNeeded"/> actually did. The console
/// uses this to decide whether to print the one-line orientation message: only
/// <see cref="Migrated"/> and <see cref="CleanedUp"/> indicate the user's files just
/// moved underneath them.
/// </summary>
public enum MigrationOutcome
{
    /// <summary>dbPath already existed at startup; nothing changed.</summary>
    NoOp,

    /// <summary>No JSON, no DB — created an empty DB.</summary>
    FreshInstall,

    /// <summary>Legacy JSON found and migrated successfully.</summary>
    Migrated,

    /// <summary>Pre-step's "both files exist" branch finished a previous, interrupted migration.</summary>
    CleanedUp,
}
