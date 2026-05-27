# Kenaz M4 — Swap JSON → SQLite Behind the Repository — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the live check-in store from JSON to SQLite without changing any behaviour visible to the journal, the views, or the export format — with a crash-safe migration that verifies the new store row-by-row and writes an importable JSON backup before retiring the old file.

**Architecture:** One new repository (`SqliteCheckInRepository`) implementing the existing `ICheckInRepository`, plus one one-shot migrator (`JsonToSqliteMigrator`) invoked from `Program.Main` before the journal is constructed. Migration uses a sentinel path (`dbPath + ".migrating"`) and an atomic `File.Move` promotion; full readback equality is the verification gate; the backup is written via `JsonCheckInArchive.Export` so it's importable through the existing menu option 5. The journal, the insight functions, the view code, and the export format are not touched.

**Tech Stack:** C# / net10.0, `Microsoft.Data.Sqlite`, NUnit 4.x. One new NuGet package on `Kenaz.Core.csproj`.

---

## Context

M1 shipped the daily tool with JSON persistence. M2 formalized the repository seam and added portable JSON export/import beside it. M3 layered insight functions on top. Through three milestones, `WellbeingJournal` has only ever known about `LoadAll()` / `SaveAll(list)`. M4 collects on the promise that seam was built for: swap the backing store from JSON to SQLite without touching anything above the seam.

Design spec (read this first): [2026-05-27-kenaz-m4-sqlite-design.md](../specs/2026-05-27-kenaz-m4-sqlite-design.md). All decisions, the migration flow, the schema, the locale-safety rules, the exception-translation policy, and the test list are pinned there.

Hardening invariants in scope:
- **#1 (atomic writes + corrupt-file recovery)** — carried over to SQLite as a symmetric `*.corrupt-*.bak` recovery on the new repository.
- **#2 (no data loss on storage swap)** — sentinel-path migration + full readback verification + importable export-format backup, with pre-step crash-recovery on the next launch.
- **#4 (one-per-date)** — enforced at the DB level by the `Date PRIMARY KEY`, so the JSON repo's dedup loop isn't needed in the new code.
- **#9 (plaintext-at-rest, accepted trade-off through M1–M5)** — unchanged; the README literacy bullet acknowledges the backup file.

**Learning mode applies** (per original spec): each task carries a short `> Concept:` note on the new C# / SQLite idea before the code.

---

## File Structure

**Create:**
- `Kenaz.Core/Storage/SqliteCheckInRepository.cs` — the new live store; implements `ICheckInRepository`.
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs` — one-shot migrator + `MigrationOutcome` enum.
- `Kenaz.Core/Storage/MigrationException.cs` — narrow exception type for the migration boundary.
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs` — full coverage of the new repository.
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs` — full coverage of the migrator.

**Modify:**
- `Kenaz.Core/Kenaz.Core.csproj` — add `Microsoft.Data.Sqlite` package; add `InternalsVisibleTo("Kenaz.Tests")`.
- `Kenaz.Core/Storage/ICheckInRepository.cs` — no behaviour change; doc comment tightened (M2 already mentioned the swap).
- `Kenaz.Core/Storage/JsonCheckInRepository.cs` — doc comment updated to note that the class is now a migration-source helper, not the live store.
- `Kenaz.Console/Program.cs` — wire the migrator, catch `MigrationException`, print the orientation line on `Migrated`/`CleanedUp`, switch the live repo to `SqliteCheckInRepository`.
- `README.md` — update the "Data" section to describe the new live store, the backup file(s), and the corrupt-file recovery convention.

**Reuse (do not duplicate the ideas, follow the patterns):**
- The temp-folder per-test fixture pattern from [Kenaz.Tests/JsonCheckInRepositoryTests.cs](../../Kenaz.Tests/JsonCheckInRepositoryTests.cs) — `Path.Combine(Path.GetTempPath(), "kenaz-tests-" + Guid.NewGuid().ToString("N"))`, `[SetUp]` creates it, `[TearDown]` deletes it recursively.
- The `BackUpCorruptFile()` shape on [JsonCheckInRepository.cs:103-108](../../Kenaz.Core/Storage/JsonCheckInRepository.cs) — `DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")` inline, no test-clock injection needed (matches the M1 precedent).
- The `JsonCheckInArchive.Export` / `Import` API ([Kenaz.Core/Storage/JsonCheckInArchive.cs](../../Kenaz.Core/Storage/JsonCheckInArchive.cs)) — the migrator reuses `Export` to write the backup file, and the migrator's recovery branch reuses the existing `JsonCheckInRepository.LoadAll()` for the corrupt-safe read of the legacy file.

> **Commits:** one task = one commit, made in **GitHub Desktop** (no `Co-Authored-By`). Push is Desktop-only and your call. Commit messages below are suggestions.

---

### Task 1: Add `Microsoft.Data.Sqlite` package + `InternalsVisibleTo` + small types

**Files:**
- Modify: `Kenaz.Core/Kenaz.Core.csproj`
- Create: `Kenaz.Core/Storage/MigrationException.cs`
- Create: `Kenaz.Core/Storage/MigrationOutcome.cs`

> Concept: `Microsoft.Data.Sqlite` is the lightweight ADO.NET provider Microsoft ships for SQLite — one NuGet, raw SQL, no DbContext or migration framework. `InternalsVisibleTo` is an assembly-level attribute (declared in the .csproj for SDK-style projects via `<InternalsVisibleTo Include="..." />`) that lets a named assembly see your `internal` members. We use it so the test project can call the migrator's `internal` test-seam overload without making it public.

- [ ] **Step 1: Add the NuGet package and `InternalsVisibleTo` to `Kenaz.Core.csproj`**

Replace the contents of `Kenaz.Core/Kenaz.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Kenaz.Tests" />
  </ItemGroup>

</Project>
```

(If `dotnet` resolves a newer 10.x line for `Microsoft.Data.Sqlite` cleanly, prefer that; otherwise the 9.0.x family is the latest stable at time of writing and works on net10.0.)

- [ ] **Step 2: Restore packages**

Run: `dotnet restore Kenaz.slnx`
Expected: succeeds, downloads `Microsoft.Data.Sqlite` and its transitive dependencies (`SQLitePCLRaw.*`).

- [ ] **Step 3: Create `MigrationException.cs`**

Create `Kenaz.Core/Storage/MigrationException.cs`:

```csharp
namespace Kenaz.Core;

/// <summary>
/// Thrown by <see cref="JsonToSqliteMigrator"/> when the migration cannot be completed
/// safely. Carries the original exception (if any) as <see cref="Exception.InnerException"/>
/// so the console caller can show a warm message without losing diagnostic detail.
/// </summary>
public class MigrationException : Exception
{
    public MigrationException(string message) : base(message) { }

    public MigrationException(string message, Exception innerException) : base(message, innerException) { }
}
```

- [ ] **Step 4: Create `MigrationOutcome.cs`**

Create `Kenaz.Core/Storage/MigrationOutcome.cs`:

```csharp
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
```

- [ ] **Step 5: Build to confirm**

Run: `dotnet build Kenaz.slnx`
Expected: 0 warnings, 0 errors. `Microsoft.Data.Sqlite` is on the build path; the two new types compile.

- [ ] **Step 6: Commit**

Commit message: `feat(M4): add Microsoft.Data.Sqlite + MigrationException/MigrationOutcome scaffolding`

Stage in GitHub Desktop:
- `Kenaz.Core/Kenaz.Core.csproj`
- `Kenaz.Core/Storage/MigrationException.cs`
- `Kenaz.Core/Storage/MigrationOutcome.cs`

---

### Task 2: `SqliteCheckInRepository` skeleton + `DefaultFilePath`

**Files:**
- Create: `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- Create: `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

> Concept: each operation on a SQLite connection in `Microsoft.Data.Sqlite` follows the same shape — `new SqliteConnection(connectionString); conn.Open(); using var cmd = conn.CreateCommand(); cmd.CommandText = "..."; cmd.ExecuteNonQuery();` (or `ExecuteReader()` for SELECTs). The `using` statement disposes the connection at the end of the scope, which closes it cleanly. The connection string for a file-backed SQLite DB is just `"Data Source=path/to/file.db"` — that's the whole thing.

- [ ] **Step 1: Write the failing test for `DefaultFilePath`**

Create `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SqliteCheckInRepositoryTests"`
Expected: FAIL — "The type or namespace name 'SqliteCheckInRepository' could not be found."

- [ ] **Step 3: Create the skeleton class**

Create `Kenaz.Core/Storage/SqliteCheckInRepository.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SqliteCheckInRepositoryTests.DefaultFilePath"`
Expected: PASS.

- [ ] **Step 5: Build to confirm nothing else broke**

Run: `dotnet build Kenaz.slnx`
Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

Commit message: `feat(M4): SqliteCheckInRepository skeleton + DefaultFilePath`

Stage:
- `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

---

### Task 3: Constructor creates the schema; `LoadAll` returns empty on a new DB

**Files:**
- Modify: `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- Modify: `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

> Concept: `CREATE TABLE IF NOT EXISTS` is idempotent — running it on a fresh file creates the table; running it on a file that already has the table is a no-op. So we can run it every time the constructor opens, and never worry about a separate "initialize" step. The same connection string opens the same file every time, and SQLite creates the file on first `Open()` if it doesn't exist yet.

- [ ] **Step 1: Write the failing tests**

Append to `Kenaz.Tests/SqliteCheckInRepositoryTests.cs` (inside the existing class):

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SqliteCheckInRepositoryTests"`
Expected: FAIL — `NotImplementedException` from `LoadAll`, plus the schema test will pass File.Exists trivially but the construction-twice will also fail at LoadAll if it runs there. (Specifically, `Schema_is_created_on_first_open` may pass File.Exists because the constructor opens the connection… actually, it currently doesn't open at all. So File.Exists will be False. That's the expected failure.)

- [ ] **Step 3: Implement constructor schema creation + LoadAll**

Replace the body of `SqliteCheckInRepository` with the connection logic. The full file now reads:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SqliteCheckInRepositoryTests"`
Expected: PASS for `Schema_is_created_on_first_open`, `LoadAll_returns_empty_when_db_is_new`, and `DefaultFilePath_ends_with_Kenaz_checkins_db` (3 tests).

- [ ] **Step 5: Commit**

Commit message: `feat(M4): SqliteCheckInRepository constructor creates schema; LoadAll empty case`

Stage:
- `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

---

### Task 4: `SaveAll` writes all fields; `LoadAll` round-trips them

**Files:**
- Modify: `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- Modify: `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

> Concept: a SQLite transaction (`conn.BeginTransaction()`) bundles multiple writes into one atomic unit — either every row inside the transaction is visible, or none are. Combined with `using var tx = ...`, if anything throws before `tx.Commit()`, the transaction rolls back when it's disposed. The pattern is `BEGIN; DELETE FROM CheckIns; INSERT ...; INSERT ...; COMMIT;` — a clean way to make "here's the full list, persist it" atomic. Bound parameters (`$date`, `$mood`, …) are the standard ADO.NET defence against SQL injection; they also handle type coercion for you.

- [ ] **Step 1: Write the failing test**

Append to `SqliteCheckInRepositoryTests`:

```csharp
private static readonly DateOnly Day = new DateOnly(2026, 5, 22);
private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.FromHours(2));
private static readonly DateTimeOffset Updated = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.FromHours(2));

[Test]
public void SaveAll_then_LoadAll_round_trips_all_fields_including_timestamps()
{
    var repository = new SqliteCheckInRepository(_filePath);
    var checkIn = new CheckIn(Day, mood: 7, energy: 6, sleep: 7.5m, note: "ok", createdAt: Created, updatedAt: Updated);

    repository.SaveAll(new[] { checkIn });
    var loaded = repository.LoadAll();

    Assert.That(loaded, Has.Count.EqualTo(1));
    Assert.That(loaded[0].Date, Is.EqualTo(Day));
    Assert.That(loaded[0].Mood, Is.EqualTo(7));
    Assert.That(loaded[0].Energy, Is.EqualTo(6));
    Assert.That(loaded[0].Sleep, Is.EqualTo(7.5m));
    Assert.That(loaded[0].Note, Is.EqualTo("ok"));
    Assert.That(loaded[0].CreatedAt, Is.EqualTo(Created));
    Assert.That(loaded[0].UpdatedAt, Is.EqualTo(Updated));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~SaveAll_then_LoadAll_round_trips"`
Expected: FAIL — `NotImplementedException` from `SaveAll`.

- [ ] **Step 3: Implement `SaveAll`**

Replace the `SaveAll` method in `SqliteCheckInRepository.cs` (and add the helper for parameter assignment) with:

```csharp
public void SaveAll(IReadOnlyList<CheckIn> checkIns)
{
    var directory = Path.GetDirectoryName(_filePath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    using var conn = new SqliteConnection(_connectionString);
    conn.Open();
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~SaveAll_then_LoadAll_round_trips"`
Expected: PASS.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: All existing tests still pass; the 4 new SQLite tests pass.

- [ ] **Step 6: Commit**

Commit message: `feat(M4): SqliteCheckInRepository SaveAll round-trips all fields`

Stage:
- `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

---

### Task 5: `SaveAll` / `LoadAll` edge cases (precision, nulls, replace, empty, duplicate-dates rollback)

**Files:**
- Modify: `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

> Concept: this task adds tests that *should already pass* with the implementation from Task 4 — they pin invariants the spec promises (decimal precision via TEXT storage, null preservation via `DBNull.Value`, DELETE+INSERT replacement semantics, transactional rollback on PK conflict). Adding them as failing-first tests would mean inventing breakage; instead, write each, run them, and confirm they pass. If any *don't* pass, that's a real defect in Task 4's implementation that needs to be tracked down before moving on.

- [ ] **Step 1: Write all five tests**

Append to `SqliteCheckInRepositoryTests`:

```csharp
[Test]
public void SaveAll_then_LoadAll_preserves_decimal_precision()
{
    var repository = new SqliteCheckInRepository(_filePath);
    var checkIn = new CheckIn(Day, mood: null, energy: null, sleep: 7.5m, note: null, createdAt: Created, updatedAt: Created);

    repository.SaveAll(new[] { checkIn });
    var loaded = repository.LoadAll();

    // The TEXT storage path preserves 7.5 exactly; REAL would risk 7.4999...
    Assert.That(loaded[0].Sleep, Is.EqualTo(7.5m));
}

[Test]
public void SaveAll_then_LoadAll_preserves_null_fields()
{
    var repository = new SqliteCheckInRepository(_filePath);
    // Only mood is set — every other optional field stays null (never 0, never "").
    var checkIn = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

    repository.SaveAll(new[] { checkIn });
    var loaded = repository.LoadAll();

    Assert.That(loaded[0].Mood, Is.EqualTo(5));
    Assert.That(loaded[0].Energy, Is.Null);
    Assert.That(loaded[0].Sleep, Is.Null);
    Assert.That(loaded[0].Note, Is.Null);
}

[Test]
public void SaveAll_replaces_previous_state()
{
    var repository = new SqliteCheckInRepository(_filePath);
    var first = new CheckIn(Day, mood: 7, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);
    var second = new CheckIn(Day.AddDays(1), mood: 3, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

    repository.SaveAll(new[] { first });
    repository.SaveAll(new[] { second });
    var loaded = repository.LoadAll();

    // The second SaveAll wins entirely — no leftover row from the first.
    Assert.That(loaded, Has.Count.EqualTo(1));
    Assert.That(loaded[0].Date, Is.EqualTo(Day.AddDays(1)));
    Assert.That(loaded[0].Mood, Is.EqualTo(3));
}

[Test]
public void SaveAll_empty_list_clears_the_table()
{
    var repository = new SqliteCheckInRepository(_filePath);
    var checkIn = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

    repository.SaveAll(new[] { checkIn });
    repository.SaveAll(Array.Empty<CheckIn>());

    Assert.That(repository.LoadAll(), Is.Empty);
}

[Test]
public void SaveAll_with_duplicate_dates_in_input_rolls_back_atomically()
{
    var repository = new SqliteCheckInRepository(_filePath);
    var seed = new CheckIn(Day, mood: 5, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);
    repository.SaveAll(new[] { seed });

    var dup1 = new CheckIn(Day.AddDays(1), mood: 1, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);
    var dup2 = new CheckIn(Day.AddDays(1), mood: 2, energy: null, sleep: null, note: null, createdAt: Created, updatedAt: Created);

    Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => repository.SaveAll(new[] { dup1, dup2 }));

    // The transaction rolled back — the seed row from the previous SaveAll is still there,
    // and no partial state from the duplicate-dates input is visible.
    var loaded = repository.LoadAll();
    Assert.That(loaded, Has.Count.EqualTo(1));
    Assert.That(loaded[0].Date, Is.EqualTo(Day));
    Assert.That(loaded[0].Mood, Is.EqualTo(5));
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~SqliteCheckInRepositoryTests"`
Expected: PASS — all 9 SQLite tests now pass. If any *fail*, the corresponding behaviour in Task 4 needs fixing before continuing.

- [ ] **Step 3: Commit**

Commit message: `test(M4): SqliteCheckInRepository edge cases (precision, nulls, replace, empty, rollback)`

Stage:
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

---

### Task 6: Corrupt-file recovery in `EnsureSchema` + `LoadAll`

**Files:**
- Modify: `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- Modify: `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

> Concept: SQLite's file format starts with a fixed 16-byte header. If the file exists but isn't valid SQLite (random bytes, a truncated DB, someone's accidental edit), `SqliteConnection.Open()` or the first query against it throws `SqliteException`. The recovery mirrors what `JsonCheckInRepository.BackUpCorruptFile` does on the JSON side: rename the bad file with a timestamped `.corrupt-*.bak` suffix and start over with an empty schema. Both the constructor (via `EnsureSchema`) and `LoadAll` get the same recovery — they're the two entry points where a corrupt file would first surface.

- [ ] **Step 1: Write the failing test**

Append to `SqliteCheckInRepositoryTests`:

```csharp
[Test]
public void Constructor_on_corrupt_db_backs_it_up_and_starts_fresh()
{
    // Plant a "DB" that's actually random bytes — opening this as SQLite throws.
    File.WriteAllBytes(_filePath, new byte[] { 0x00, 0x42, 0x69, 0x6e, 0xff, 0x00 });

    var repository = new SqliteCheckInRepository(_filePath);

    // The bad file got renamed away…
    var corruptBackups = Directory.GetFiles(_dir, "checkins.db.corrupt-*.bak");
    Assert.That(corruptBackups, Has.Length.EqualTo(1));
    // …and a fresh, empty SQLite database took its place.
    Assert.That(File.Exists(_filePath), Is.True);
    Assert.That(repository.LoadAll(), Is.Empty);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "Constructor_on_corrupt_db"`
Expected: FAIL — `SqliteException` propagates out of the constructor.

- [ ] **Step 3: Add the recovery path**

Replace `EnsureSchema` in `SqliteCheckInRepository.cs` and update `LoadAll` to wrap its work in the same recovery. Add a private `BackUpCorruptFile` helper at the bottom of the class:

```csharp
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

private IReadOnlyList<CheckIn> LoadAllCore()
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
    using var conn = new SqliteConnection(_connectionString);
    conn.Open();
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

    var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    var backupPath = _filePath + $".corrupt-{timestamp}.bak";
    File.Move(_filePath, backupPath, overwrite: true);
}
```

(The earlier inline `EnsureSchema` body — `using var conn …` — is now extracted into `OpenAndRunSchema()` so the corrupt-file retry can call it without re-typing the connection logic.)

- [ ] **Step 4: Run the failing test + the full suite**

Run: `dotnet test`
Expected: All SQLite tests pass (10 now). All existing tests still pass.

- [ ] **Step 5: Commit**

Commit message: `feat(M4): SqliteCheckInRepository corrupt-file recovery (constructor + LoadAll)`

Stage:
- `Kenaz.Core/Storage/SqliteCheckInRepository.cs`
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

---

### Task 7: Locale-safe round trip under `nb-NO`

**Files:**
- Modify: `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

> Concept: in Norwegian default culture (`nb-NO`), decimals format with comma (`7,5`) and timestamps with different separators. If we used `c.Sleep.Value.ToString()` (no culture) on a Norwegian machine, the DB would store `"7,5"`, and a later `decimal.Parse("7,5")` (no culture) on a different machine would read it as `75`. Pinning every write and read to `CultureInfo.InvariantCulture` makes the on-disk format portable. This test wraps the existing round-trip in a culture swap so the locale boundary is exercised cheaply — if any boundary in Task 4 was left unpinned, this test catches it.

- [ ] **Step 1: Write the failing test (it should pass if Task 4 was thorough)**

Append to `SqliteCheckInRepositoryTests`:

```csharp
[Test]
public void Round_trip_under_nb_NO_locale_preserves_decimal_and_timestamp()
{
    var original = System.Globalization.CultureInfo.CurrentCulture;
    var originalUI = System.Globalization.CultureInfo.CurrentUICulture;
    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nb-NO");
    System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("nb-NO");

    try
    {
        var repository = new SqliteCheckInRepository(_filePath);
        var nonUtc = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.FromHours(2));
        var checkIn = new CheckIn(Day, mood: 7, energy: null, sleep: 7.5m, note: null, createdAt: nonUtc, updatedAt: nonUtc);

        repository.SaveAll(new[] { checkIn });
        var loaded = repository.LoadAll();

        Assert.That(loaded[0].Sleep, Is.EqualTo(7.5m), "Decimal round trip must not depend on system culture");
        Assert.That(loaded[0].CreatedAt, Is.EqualTo(nonUtc), "DateTimeOffset round trip must preserve the non-UTC offset");
    }
    finally
    {
        System.Globalization.CultureInfo.CurrentCulture = original;
        System.Globalization.CultureInfo.CurrentUICulture = originalUI;
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test --filter "Round_trip_under_nb_NO"`
Expected: PASS. (If it fails, audit the SQLite code for any `.ToString()` / `.Parse()` call missing `CultureInfo.InvariantCulture` and fix it.)

- [ ] **Step 3: Commit**

Commit message: `test(M4): SqliteCheckInRepository round trip under nb-NO locale`

Stage:
- `Kenaz.Tests/SqliteCheckInRepositoryTests.cs`

---

### Task 8: `JsonToSqliteMigrator` skeleton + `NoOp` case

**Files:**
- Create: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Create: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: a `public static class` is the idiomatic shape for stateless cross-cutting helpers in C# — no instance state, no `new` required at the call site. The migrator's signature is a function from `(jsonPath, dbPath, now)` to a `MigrationOutcome`; the `internal` overload for tests adds a `verify` delegate as the seam. We start with just the public overload and the simplest case (rule 1: dbPath exists → NoOp).

- [ ] **Step 1: Write the failing test**

Create `Kenaz.Tests/JsonToSqliteMigratorTests.cs`:

```csharp
using Kenaz.Core;

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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: FAIL — type does not exist.

- [ ] **Step 3: Create the skeleton**

Create `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`:

```csharp
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
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "Skips_when_db_already_exists"`
Expected: PASS.

- [ ] **Step 5: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator skeleton + NoOp case`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 9: `FreshInstall` case (neither file exists → empty DB)

**Files:**
- Modify: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: constructing `SqliteCheckInRepository` already creates the schema (Task 3), so the fresh-install branch is one line — `new SqliteCheckInRepository(dbPath);` — followed by returning `FreshInstall`. We discard the repo because the migrator doesn't keep it around; `Program.Main` will construct its own after `MigrateIfNeeded` returns.

- [ ] **Step 1: Write the failing test**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void Fresh_install_creates_empty_db_and_no_backup_returns_FreshInstall()
{
    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.FreshInstall));
    Assert.That(File.Exists(_dbPath), Is.True);
    Assert.That(new SqliteCheckInRepository(_dbPath).LoadAll(), Is.Empty);
    Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Is.Empty);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "Fresh_install_creates_empty_db"`
Expected: FAIL — `NotImplementedException`.

- [ ] **Step 3: Implement the case**

Replace the migrator body's `if (File.Exists(dbPath)) ...` block plus the throw, with:

```csharp
if (File.Exists(dbPath))
{
    return MigrationOutcome.NoOp;
}

if (!File.Exists(jsonPath))
{
    _ = new SqliteCheckInRepository(dbPath);
    return MigrationOutcome.FreshInstall;
}

throw new NotImplementedException("Migration case lands in Task 10.");
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 2 of 2 PASS.

- [ ] **Step 5: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator FreshInstall case`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 10: Happy-path migration (sentinel path → promote → export-format backup → delete JSON)

**Files:**
- Modify: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: the sentinel-path pattern is a generalisation of "write to temp file, then move" — it guarantees the live filename never points at an incomplete file. We write the new DB at `dbPath + ".migrating"`, then `File.Move(migratingPath, dbPath)` atomically promotes it. The backup is written via `JsonCheckInArchive.Export` (not a raw rename) so menu option 5 can read it back if the user ever needs to restore. The default verifier compares the two sorted lists field-by-field — `Count` first, then a per-index loop on all 7 columns, with early exit on the first difference.

- [ ] **Step 1: Write the failing tests (4 at once — they all exercise the same path)**

Append to `JsonToSqliteMigratorTests`:

```csharp
private static readonly DateOnly Day1 = new DateOnly(2026, 5, 25);
private static readonly DateOnly Day2 = new DateOnly(2026, 5, 26);

private void WriteLegacyJson(params CheckIn[] records)
{
    // Reuse the existing JSON repo to write a legacy file in the format the migrator will read.
    new JsonCheckInRepository(_jsonPath).SaveAll(records);
}

private static CheckIn MakeCheckIn(DateOnly date, int? mood = 5, decimal? sleep = 7.0m)
{
    var ts = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(2));
    return new CheckIn(date, mood, energy: null, sleep: sleep, note: null, createdAt: ts, updatedAt: ts);
}

[Test]
public void Migrates_records_from_legacy_json_into_db_returns_Migrated()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7), MakeCheckIn(Day2, mood: 4));

    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.Migrated));
    var loaded = new SqliteCheckInRepository(_dbPath).LoadAll();
    Assert.That(loaded.Count, Is.EqualTo(2));
    Assert.That(loaded.Select(c => c.Mood), Is.EquivalentTo(new int?[] { 7, 4 }));
}

[Test]
public void Writes_export_format_backup_after_successful_migration()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

    JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    var backups = Directory.GetFiles(_dir, "checkins.backup-*.json");
    Assert.That(backups, Has.Length.EqualTo(1));
    // The file should be in the envelope format, i.e. readable by JsonCheckInArchive.Import.
    var imported = new JsonCheckInArchive().Import(backups[0]);
    Assert.That(imported.Records.Count, Is.EqualTo(1));
    Assert.That(imported.Records[0].Mood, Is.EqualTo(7));
}

[Test]
public void Deletes_original_json_after_successful_migration()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

    JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(File.Exists(_jsonPath), Is.False);
}

[Test]
public void Backup_is_importable_via_archive()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7), MakeCheckIn(Day2, mood: 4));

    JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    var backupPath = Directory.GetFiles(_dir, "checkins.backup-*.json").Single();
    var imported = new JsonCheckInArchive().Import(backupPath);

    // The backup is round-trippable through the existing import path — proof of recoverability.
    Assert.That(imported.Records, Has.Count.EqualTo(2));
    Assert.That(imported.Records.Select(c => c.Date), Is.EquivalentTo(new[] { Day1, Day2 }));
}

[Test]
public void Migrated_db_round_trips_with_journal()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7), MakeCheckIn(Day2, mood: 4));

    JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    var repo = new SqliteCheckInRepository(_dbPath);
    var journal = new WellbeingJournal(repo, () => Now);
    var history = journal.History();
    Assert.That(history, Has.Count.EqualTo(2));
    Assert.That(history.Select(c => c.Mood), Is.EquivalentTo(new int?[] { 7, 4 }));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 5 tests, 3 NoOp/FreshInstall passing, 5 new ones FAILing (NotImplementedException).

- [ ] **Step 3: Implement the migration case + the real verifier**

Replace the `throw new NotImplementedException("Migration case lands in Task 10.")` in `JsonToSqliteMigrator.cs` with the real flow, and replace `DefaultVerifier` with the proper field-by-field comparison. The migrator file now reads:

```csharp
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

        if (!File.Exists(jsonPath))
        {
            _ = new SqliteCheckInRepository(dbPath);
            return MigrationOutcome.FreshInstall;
        }

        var source = new JsonCheckInRepository(jsonPath).LoadAll();
        var migratingPath = dbPath + ".migrating";

        var repo = new SqliteCheckInRepository(migratingPath);
        repo.SaveAll(source);

        var verifyList = repo.LoadAll();
        var sortedSource = source.OrderBy(c => c.Date).ToList();
        var sortedVerify = verifyList.OrderBy(c => c.Date).ToList();
        if (!verify(sortedSource, sortedVerify))
        {
            if (File.Exists(migratingPath))
            {
                File.Delete(migratingPath);
            }
            throw new MigrationException("Verification failed — the new database didn't match the source.");
        }

        File.Move(migratingPath, dbPath);

        var backupPath = Path.Combine(
            Path.GetDirectoryName(dbPath)!,
            $"checkins.backup-{now:yyyyMMdd-HHmmss}.json");
        new JsonCheckInArchive().Export(backupPath, source, now);
        File.Delete(jsonPath);

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
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 7 of 7 PASS.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: every existing test still passes; the 7 migrator tests pass.

- [ ] **Step 6: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator happy-path migration with verified-readback + export-format backup`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 11: Verification failure via the `internal` test seam

**Files:**
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: the migrator's `internal` overload takes a `verify` delegate. Production callers go through the public overload (which passes `DefaultVerifier`); the test passes a stub that always returns `false` to exercise the rollback branch. The test asserts the rollback invariants exactly: the partial `.migrating` file is gone, `jsonPath` is untouched, no `.backup-*` was written, and `MigrationException` was thrown. Because `InternalsVisibleTo("Kenaz.Tests")` is set on `Kenaz.Core.csproj` (Task 1), the test project can call the `internal` overload directly.

- [ ] **Step 1: Write the failing test**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void Verification_failure_deletes_migrating_path_and_keeps_json()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

    Assert.Throws<MigrationException>(() =>
        JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now, verify: (a, b) => false));

    Assert.That(File.Exists(_dbPath), Is.False, "Live store should never have been promoted.");
    Assert.That(File.Exists(_dbPath + ".migrating"), Is.False, "Sentinel path should be cleaned up.");
    Assert.That(File.Exists(_jsonPath), Is.True, "Source JSON must be untouched on failure.");
    Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Is.Empty, "Backup must not be written.");
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test --filter "Verification_failure_deletes_migrating_path"`
Expected: PASS (the rollback branch from Task 10 is already in place; this test just exercises it via the test seam).

- [ ] **Step 3: Commit**

Commit message: `test(M4): JsonToSqliteMigrator verification-failure rollback via internal seam`

Stage:
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 12: Corrupt legacy JSON → empty DB + corrupt-bak; migrated db round-trips with journal already covered

**Files:**
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: `JsonCheckInRepository.LoadAll()` already backs up a corrupt JSON file and returns empty. The migrator then "migrates" zero records into the new DB. The verifier passes (empty == empty), so promotion succeeds; the export step writes an envelope with zero records; the original (already-renamed-to-`.bak`) JSON is no longer at `jsonPath`, so the `File.Delete(jsonPath)` after the export becomes a no-op. The whole thing exercises the same path as the happy case — we just need a test that proves it does the right thing with bad input.

- [ ] **Step 1: Write the failing test**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void Corrupt_legacy_json_results_in_empty_db_and_corrupt_backup()
{
    File.WriteAllText(_jsonPath, "{ not valid json ");

    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.Migrated));
    Assert.That(File.Exists(_dbPath), Is.True);
    Assert.That(new SqliteCheckInRepository(_dbPath).LoadAll(), Is.Empty);

    // The corrupt JSON was renamed by JsonCheckInRepository's recovery before the migrator saw it…
    var corruptBackups = Directory.GetFiles(_dir, "checkins.json.corrupt-*.bak");
    Assert.That(corruptBackups, Has.Length.EqualTo(1));
    // …and the (empty) export-format backup is also present.
    Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Has.Length.EqualTo(1));
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test --filter "Corrupt_legacy_json"`
Expected: PASS. (If the `File.Delete(jsonPath)` step throws because the file is already gone, change the migrator to `if (File.Exists(jsonPath)) File.Delete(jsonPath);` and re-run.)

> If the test fails with a `FileNotFoundException` on `File.Delete(jsonPath)`: edit `JsonToSqliteMigrator.cs` so the final delete is guarded:
> ```csharp
> if (File.Exists(jsonPath))
> {
>     File.Delete(jsonPath);
> }
> ```
> Then re-run the test.

- [ ] **Step 3: Commit**

Commit message: `test(M4): JsonToSqliteMigrator handles corrupt legacy JSON via existing recovery`

Stage:
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs` (if the guard was added)

---

### Task 13: Pre-step — orphan `.migrating` cleanup

**Files:**
- Modify: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: if a previous migration was killed between writing the sentinel file and promoting it, `dbPath + ".migrating"` exists at startup as an orphan. We delete it unconditionally at the very top of the migrator, before the main decision tree runs — fresh start every time. The check is `File.Delete`-safe because we wrap it in a `File.Exists` guard.

- [ ] **Step 1: Write the failing test**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void Orphan_migrating_file_is_cleaned_up_then_migration_proceeds()
{
    // Stage: an orphan .migrating file + a legacy JSON. Mimics an interrupted previous run.
    File.WriteAllBytes(_dbPath + ".migrating", new byte[] { 0xde, 0xad });
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.Migrated));
    Assert.That(File.Exists(_dbPath + ".migrating"), Is.False, "Orphan must be deleted before migration runs.");
    Assert.That(new SqliteCheckInRepository(_dbPath).LoadAll(), Has.Count.EqualTo(1));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "Orphan_migrating_file"`
Expected: FAIL — likely a `SqliteException` from constructing `SqliteCheckInRepository(migratingPath)` against the byte garbage, or a `File.Move` failure when promoting.

- [ ] **Step 3: Add the pre-step at the top of the `internal` overload**

Edit `JsonToSqliteMigrator.cs`'s `internal MigrateIfNeeded` so the body begins with:

```csharp
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
    // … rest of the method unchanged (it can stop redeclaring `migratingPath` since it's already in scope)
```

Adjust the body of the migrate case so it does NOT redeclare `migratingPath` (it's already in scope from the pre-step). The rest of the method stays the same.

- [ ] **Step 4: Run the test**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 9 of 9 PASS.

- [ ] **Step 5: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator pre-step cleans up orphan .migrating files`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 14: Pre-step — `both files exist` cleanup → `CleanedUp`

**Files:**
- Modify: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: this is the recovery branch for a migration that crashed between promoting the new DB (step 5) and writing the backup (step 6) — both files end up on disk. SQLite is already the live store; we just need to finish the cleanup: read the JSON via the corrupt-safe `JsonCheckInRepository`, write the backup in export envelope format, delete the JSON. Returning `MigrationOutcome.CleanedUp` (not `NoOp`) is what causes `Program.Main` to print the orientation line on the run that finishes the cleanup — so the user is told "the JSON is now a backup, here's its name" even though they're a launch later than the original migration.

- [ ] **Step 1: Write the failing tests (three at once — they share the same code path)**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void Both_dbPath_and_jsonPath_exist_triggers_cleanup_returns_CleanedUp()
{
    // Pre-stage a populated SQLite and a separate-content legacy JSON.
    var dbRecord = MakeCheckIn(Day1, mood: 7);
    new SqliteCheckInRepository(_dbPath).SaveAll(new[] { dbRecord });
    WriteLegacyJson(MakeCheckIn(Day2, mood: 4));

    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.CleanedUp));
    Assert.That(File.Exists(_jsonPath), Is.False, "JSON must be deleted after backup is written.");
    var backups = Directory.GetFiles(_dir, "checkins.backup-*.json");
    Assert.That(backups, Has.Length.EqualTo(1));
    var imported = new JsonCheckInArchive().Import(backups[0]);
    Assert.That(imported.Records[0].Mood, Is.EqualTo(4), "Backup contains the JSON's records, not the DB's.");

    // DB contents untouched.
    var dbLoaded = new SqliteCheckInRepository(_dbPath).LoadAll();
    Assert.That(dbLoaded.Single().Mood, Is.EqualTo(7));
}

[Test]
public void Both_files_present_with_unreadable_json_still_completes_cleanup()
{
    // SQLite already populated; JSON file is unreadable garbage.
    new SqliteCheckInRepository(_dbPath).SaveAll(new[] { MakeCheckIn(Day1, mood: 7) });
    File.WriteAllText(_jsonPath, "{ this is not valid json ");

    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.CleanedUp));
    Assert.That(File.Exists(_jsonPath), Is.False);
    // JsonCheckInRepository's corrupt-recovery renames the bad file…
    Assert.That(Directory.GetFiles(_dir, "checkins.json.corrupt-*.bak"), Has.Length.EqualTo(1));
    // …and the cleanup still writes an (empty-records) backup envelope.
    Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Has.Length.EqualTo(1));
}

[Test]
public void Cleanup_backup_uses_now_timestamp_when_an_earlier_backup_exists()
{
    // Pre-existing backup from some earlier interrupted run.
    var earlierBackup = Path.Combine(_dir, "checkins.backup-20260101-000000.json");
    File.WriteAllText(earlierBackup, "{}"); // contents don't matter for this test
    new SqliteCheckInRepository(_dbPath).SaveAll(new[] { MakeCheckIn(Day1, mood: 7) });
    WriteLegacyJson(MakeCheckIn(Day2, mood: 4));

    JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    var allBackups = Directory.GetFiles(_dir, "checkins.backup-*.json");
    Assert.That(allBackups, Has.Length.EqualTo(2), "Earlier backup + new backup coexist as harmless duplicates.");
    Assert.That(allBackups.Any(p => p.Contains(Now.ToString("yyyyMMdd-HHmmss"))), Is.True);
    Assert.That(allBackups.Any(p => p.EndsWith("20260101-000000.json")), Is.True);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 3 new tests FAIL. The "both files exist" branch falls into the rule-1 `NoOp` case currently, which trips the assertions.

- [ ] **Step 3: Add the pre-step "both files exist" branch**

Edit `JsonToSqliteMigrator.cs`'s `internal MigrateIfNeeded` body so the pre-step has two branches (orphan delete + both-files cleanup), and the cleanup branch short-circuits with `CleanedUp` instead of falling through:

```csharp
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

    // Main decision tree.
    if (File.Exists(dbPath))
    {
        return MigrationOutcome.NoOp;
    }

    if (!File.Exists(jsonPath))
    {
        _ = new SqliteCheckInRepository(dbPath);
        return MigrationOutcome.FreshInstall;
    }

    // Case 3: migrate via the sentinel-path pattern.
    var source = new JsonCheckInRepository(jsonPath).LoadAll();

    var repo = new SqliteCheckInRepository(migratingPath);
    repo.SaveAll(source);

    var verifyList = repo.LoadAll();
    var sortedSource = source.OrderBy(c => c.Date).ToList();
    var sortedVerify = verifyList.OrderBy(c => c.Date).ToList();
    if (!verify(sortedSource, sortedVerify))
    {
        if (File.Exists(migratingPath))
        {
            File.Delete(migratingPath);
        }
        throw new MigrationException("Verification failed — the new database didn't match the source.");
    }

    File.Move(migratingPath, dbPath);

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
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 12 of 12 PASS.

- [ ] **Step 5: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator pre-step cleanup for "both files exist" → CleanedUp`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 15: Exception translation across step 3.3 → step 5

**Files:**
- Modify: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: when `SaveAll` throws `SqliteException` (e.g. disk full) or `File.Move` throws `IOException` (e.g. TOCTOU race where `dbPath` appeared between the check and the move), we don't want a raw stack trace to surface in the console. We catch both, clean up the `.migrating` sentinel, and re-throw as `MigrationException` so the existing warm-error path in `Program.Main` takes over. The catch surrounds steps 3.3 through 5 inclusive — once step 5 returns, the live store is committed and any later exception is the "swallow" branch (Task 16), not this one.

- [ ] **Step 1: Write the failing test**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void SaveAll_failure_translates_to_MigrationException_and_cleans_up()
{
    // Stage a JSON file with a duplicate-date pair the SQLite PK will reject.
    // We can't easily inject SaveAll failure from the outside, but we can stage a JSON
    // file whose deserialized records would trip the SQLite PK on insert — except that
    // JsonCheckInRepository already dedups on load. So we use an alternative path: place
    // a *directory* at the migratingPath so File.Move during promotion throws IOException.
    var migratingPath = _dbPath + ".migrating";
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7));
    // The directory blocks SaveAll from creating a writable SQLite file at the sentinel path.
    Directory.CreateDirectory(migratingPath);

    var ex = Assert.Throws<MigrationException>(() =>
        JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now));

    Assert.That(ex!.InnerException, Is.Not.Null, "MigrationException should wrap the underlying exception.");
    Assert.That(File.Exists(_dbPath), Is.False, "dbPath must not have been promoted.");
    Assert.That(File.Exists(_jsonPath), Is.True, "Source JSON must survive.");
    Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Is.Empty);

    // Tidy up the directory we created so [TearDown] can clean the temp folder.
    Directory.Delete(migratingPath, recursive: true);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "SaveAll_failure_translates"`
Expected: FAIL — `SqliteException` (or `IOException`) propagates out unwrapped.

- [ ] **Step 3: Wrap steps 3.3 → 5 in try/catch translation**

Edit the case-3 block in `JsonToSqliteMigrator.cs`:

```csharp
// Case 3: migrate via the sentinel-path pattern.
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
        if (File.Exists(migratingPath))
        {
            File.Delete(migratingPath);
        }
        throw new MigrationException("Verification failed — the new database didn't match the source.");
    }

    File.Move(migratingPath, dbPath);
}
catch (SqliteException ex)
{
    if (File.Exists(migratingPath))
    {
        File.Delete(migratingPath);
    }
    throw new MigrationException("Couldn't write the new database — check disk space and try again.", ex);
}
catch (IOException ex)
{
    if (File.Exists(migratingPath))
    {
        File.Delete(migratingPath);
    }
    throw new MigrationException("Couldn't promote the new database — check filesystem and try again.", ex);
}

// Steps 6+7 follow here (still unwrapped; that's the next task).
var backupPath = Path.Combine(
    Path.GetDirectoryName(dbPath)!,
    $"checkins.backup-{now:yyyyMMdd-HHmmss}.json");
new JsonCheckInArchive().Export(backupPath, source, now);
if (File.Exists(jsonPath))
{
    File.Delete(jsonPath);
}

return MigrationOutcome.Migrated;
```

> Note: the existing `Verification_failure_deletes_migrating_path_and_keeps_json` test (from Task 11) still works because the `throw new MigrationException(...)` after a `false` verify is inside the `try`, and `MigrationException` is not `SqliteException` or `IOException`, so neither `catch` block intercepts it — it propagates out of the `try` cleanly. (Re-run that test to confirm.)

- [ ] **Step 4: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 13 of 13 PASS.

- [ ] **Step 5: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator translates SqliteException/IOException to MigrationException`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 16: Swallow `IOException` across step 6 → step 7; next-launch pre-step recovers

**Files:**
- Modify: `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- Modify: `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

> Concept: once step 5 has promoted the new DB, the user's data is safe in SQLite. The backup file and the JSON delete are "nice-to-have, eventually" — if they fail right now (disk full, antivirus lock, transient I/O), we shouldn't crash the app or hide the success. We catch `IOException` on the export + delete, swallow it, and still return `Migrated`. The user sees the orientation line on this run; the next launch's pre-step (the "both files exist" branch from Task 14) writes the backup and deletes the JSON. The flow self-heals.

- [ ] **Step 1: Write the failing test**

Append to `JsonToSqliteMigratorTests`:

```csharp
[Test]
public void Backup_write_failure_after_promotion_still_returns_Migrated_and_recovers_next_launch()
{
    WriteLegacyJson(MakeCheckIn(Day1, mood: 7));

    // Stage a *directory* at the backup file's target path so JsonCheckInArchive.Export
    // can't write a regular file there (IOException). Use the same timestamp the migrator
    // will use, derived from `Now`.
    var blockingBackupPath = Path.Combine(_dir, $"checkins.backup-{Now:yyyyMMdd-HHmmss}.json");
    Directory.CreateDirectory(blockingBackupPath);

    var outcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, Now);

    Assert.That(outcome, Is.EqualTo(MigrationOutcome.Migrated), "Live store is promoted; the backup hiccup is swallowed.");
    Assert.That(File.Exists(_dbPath), Is.True);
    Assert.That(new SqliteCheckInRepository(_dbPath).LoadAll(), Has.Count.EqualTo(1));
    Assert.That(File.Exists(_jsonPath), Is.True, "JSON not deleted because the backup didn't land.");

    // Clear the blocking directory and re-run; pre-step's 'both files exist' branch finishes the job.
    Directory.Delete(blockingBackupPath, recursive: true);
    var secondLaunch = new DateTimeOffset(Now.UtcDateTime.AddSeconds(1), TimeSpan.Zero);
    var secondOutcome = JsonToSqliteMigrator.MigrateIfNeeded(_jsonPath, _dbPath, secondLaunch);

    Assert.That(secondOutcome, Is.EqualTo(MigrationOutcome.CleanedUp));
    Assert.That(File.Exists(_jsonPath), Is.False);
    Assert.That(Directory.GetFiles(_dir, "checkins.backup-*.json"), Has.Length.EqualTo(1));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "Backup_write_failure_after_promotion"`
Expected: FAIL — `IOException` from `Export` propagates out and the test sees the wrong exception type.

- [ ] **Step 3: Add the swallow**

In `JsonToSqliteMigrator.cs`, wrap the post-promote backup + delete in a `try/catch (IOException)` that swallows. The case-3 block now ends with:

```csharp
    // (above: try/catch around SaveAll → verify → File.Move from Task 15)

    // Steps 6+7: best-effort backup + delete. Live store already committed.
    try
    {
        var backupPath = Path.Combine(
            Path.GetDirectoryName(dbPath)!,
            $"checkins.backup-{now:yyyyMMdd-HHmmss}.json");
        new JsonCheckInArchive().Export(backupPath, source, now);
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
        }
    }
    catch (IOException)
    {
        // Live store is the source of truth. Pre-step on the next launch's "both files exist"
        // branch will retry the backup write and the JSON delete.
    }

    return MigrationOutcome.Migrated;
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --filter "FullyQualifiedName~JsonToSqliteMigratorTests"`
Expected: 14 of 14 PASS.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test`
Expected: every test in the solution passes.

- [ ] **Step 6: Commit**

Commit message: `feat(M4): JsonToSqliteMigrator swallows post-promote IOException; recovers on next launch`

Stage:
- `Kenaz.Core/Storage/JsonToSqliteMigrator.cs`
- `Kenaz.Tests/JsonToSqliteMigratorTests.cs`

---

### Task 17: Wire the migrator into `Program.Main` + orientation line + `MigrationException` catch

**Files:**
- Modify: `Kenaz.Console/Program.cs`

> Concept: this is the controller change. The wiring order is: derive both paths → call `MigrateIfNeeded` inside a `try/catch (MigrationException)` → on `Migrated` or `CleanedUp`, print the orientation line → construct the live `SqliteCheckInRepository` → carry on into the existing greeting + menu loop. The `MigrationException` catch is the warm-error path: print two friendly lines, return from `Main` (which exits the app without constructing the journal).

- [ ] **Step 1: Read the current Program.cs to confirm the insertion points**

(No action — just orient yourself. The current `Main` constructs `new JsonCheckInRepository(JsonCheckInRepository.DefaultFilePath())` then a `WellbeingJournal` then prints the greeting; everything after that stays.)

- [ ] **Step 2: Replace the top of `Main`**

Open `Kenaz.Console/Program.cs`. Locate the existing block that begins at `System.Console.OutputEncoding = ...;` and runs through the `WriteLine("Kenaz — your daily check-in. Bring it into the light.");` line. Replace it with:

```csharp
System.Console.OutputEncoding = System.Text.Encoding.UTF8;

var jsonPath = JsonCheckInRepository.DefaultFilePath();
var dbPath   = SqliteCheckInRepository.DefaultFilePath();

MigrationOutcome outcome;
try
{
    outcome = JsonToSqliteMigrator.MigrateIfNeeded(jsonPath, dbPath, DateTimeOffset.Now);
}
catch (MigrationException ex)
{
    WriteLine("Couldn't safely move your check-ins to the new store — your data is untouched.");
    WriteLine($"Details: {ex.Message}");
    return;
}

if (outcome == MigrationOutcome.Migrated || outcome == MigrationOutcome.CleanedUp)
{
    WriteLine("Your check-ins have moved to a new file (checkins.db).");
    WriteLine("The previous JSON is preserved as checkins.backup-YYYYMMDD-HHMMSS.json — safe to delete once you've confirmed everything's intact.");
    WriteLine();
}

var repository = new SqliteCheckInRepository(dbPath);
var journal = new WellbeingJournal(repository, () => DateTimeOffset.Now);

WriteLine("Kenaz — your daily check-in. Bring it into the light.");
```

(Everything below the greeting — the `while (running)` loop, the menu, the option handlers, the helper methods — stays exactly as it is.)

- [ ] **Step 3: Build to confirm it compiles**

Run: `dotnet build Kenaz.slnx`
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Manual verification — fresh install**

Make sure `%APPDATA%\Kenaz\` has neither `checkins.json` nor `checkins.db` (rename or move any existing files aside first).

Run: `dotnet run --project Kenaz.Console`
Expected:
- No orientation line printed.
- The standard greeting and menu appear.
- Option 1 → check in for today → save → exit.
- A `%APPDATA%\Kenaz\checkins.db` file now exists.

Exit the app.

- [ ] **Step 5: Manual verification — second launch is silent**

Run: `dotnet run --project Kenaz.Console` again.
Expected:
- No orientation line.
- Option 3 (History) shows the check-in saved in Step 4.

- [ ] **Step 6: Manual verification — migrate a legacy JSON**

In a separate scratch directory, build a tiny `checkins.json` with a couple of check-ins (or use the test's `WriteLegacyJson` helper to construct one). Copy it to `%APPDATA%\Kenaz\checkins.json` (alongside the existing `.db`). Delete the existing `.db` (rename it aside so you can compare later).

Run: `dotnet run --project Kenaz.Console`
Expected:
- Orientation line prints: "Your check-ins have moved to a new file (checkins.db). The previous JSON is preserved as checkins.backup-…json — safe to delete once you've confirmed everything's intact."
- `checkins.json` is gone.
- `checkins.backup-YYYYMMDD-HHMMSS.json` is in the folder.
- Option 3 (History) shows the migrated check-ins.
- Option 5 (Import) against the backup file reports `0 added`, `0 updated`, `<N> unchanged` (round-trip proof).

- [ ] **Step 7: Manual verification — `MigrationException` warm path**

Stage a directory at `%APPDATA%\Kenaz\checkins.db.migrating` (this would normally be cleaned up by the pre-step, but a directory blocks the `File.Delete` and then blocks the sentinel write). Also stage a legacy JSON next to it. Move the `.db` out of the way first.

Run: `dotnet run --project Kenaz.Console`
Expected:
- Two warm lines print: "Couldn't safely move your check-ins to the new store — your data is untouched." + "Details: …"
- App exits without entering the menu.
- `checkins.json` is still in place.

Clean up the staging by deleting the directory.

- [ ] **Step 8: Commit**

Commit message: `feat(M4): wire migrator + orientation line into Program.Main; switch live store to SQLite`

Stage:
- `Kenaz.Console/Program.cs`

---

### Task 18: Update `JsonCheckInRepository` doc comment to reflect its migration-helper role

**Files:**
- Modify: `Kenaz.Core/Storage/JsonCheckInRepository.cs`

> Concept: the class still does the same thing — read + write JSON, with corrupt-file recovery. But after M4 it's no longer the live store; it's the migration source. Tightening the doc comment keeps a future reader from being confused about why the class still exists.

- [ ] **Step 1: Update the doc comment**

Open `Kenaz.Core/Storage/JsonCheckInRepository.cs`. Replace the existing `<summary>` block above the class with:

```csharp
/// <summary>
/// Reads and writes check-ins as a JSON file. Up through M3 this was the live store;
/// from M4 onward the live store is <see cref="SqliteCheckInRepository"/> and this
/// class is used only as the corrupt-safe reader of the legacy file during a one-shot
/// JSON → SQLite migration (see <see cref="JsonToSqliteMigrator"/>). Writes are atomic
/// (temp file + move) so a crash never leaves a half-written file, and loads are
/// recoverable: corrupt files are set aside and every record is re-validated and
/// de-duped by date.
/// </summary>
```

- [ ] **Step 2: Build + run tests**

Run: `dotnet build Kenaz.slnx`
Expected: 0 warnings, 0 errors.

Run: `dotnet test`
Expected: all tests still pass.

- [ ] **Step 3: Commit**

Commit message: `docs(M4): note JsonCheckInRepository is now a migration helper`

Stage:
- `Kenaz.Core/Storage/JsonCheckInRepository.cs`

---

### Task 19: Update the README

**Files:**
- Modify: `README.md`

> Concept: README literacy for the new store and the backup file(s). Three bullets in the Data section: the live store is now SQLite, the backup file(s) explanation (possibly multiple, all importable), and the `*.corrupt-*.bak` recovery convention.

- [ ] **Step 1: Replace the "Data" section**

Open `README.md`. Replace the existing section under `## Data` (everything from the first paragraph after the heading down to the line that says the design spec path, *not including* the design-spec line itself) with:

```markdown
Check-ins are stored locally as a SQLite database in `%APPDATA%\Kenaz\checkins.db`. Nothing leaves your machine.

Export saves all your check-ins to `Documents\Kenaz\kenaz-backup-<timestamp>.json`; import merges a backup back in, where the more recently edited entry wins so a restore never overwrites newer changes. The export file is plain, unencrypted JSON — keep it somewhere private.

### Files you may see in `%APPDATA%\Kenaz\`

- `checkins.db` — the live store.
- `checkins.backup-YYYYMMDD-HHMMSS.json` — written by the JSON → SQLite migration, in the same format as a normal export. You may occasionally see more than one if a previous migration was interrupted; they contain the same historic data (the timestamp in the filename tells you which is which), and any of them is importable via menu option 5 if you ever need to restore. Plaintext (same caveat as exports); safe to delete once you've confirmed your check-ins are intact in the new store (option 3 or 6).
- `checkins.json.corrupt-YYYYMMDD-HHMMSS.bak` / `checkins.db.corrupt-YYYYMMDD-HHMMSS.bak` — Kenaz sets the bad file aside (with this name) if it can't read it on startup, then starts with a fresh empty store. If you see one, the matching live file (`checkins.json` or `checkins.db`) was unreadable; the `.bak` is your last-known-good copy.
```

(The "Design spec" line stays where it is.)

- [ ] **Step 2: Update the intro paragraph if it mentions JSON storage explicitly**

Check the very top of the README. The line "C# solution with a domain core, a console front-end, and an NUnit test project — growing toward a SQLite store, ..." should be updated since the SQLite store has now arrived. Replace with:

```markdown
C# solution with a domain core, a console front-end, and an NUnit test project — built around a SQLite store, with an ASP.NET Minimal API and a mobile-first PWA on the roadmap.
```

- [ ] **Step 3: Commit**

Commit message: `docs(M4): README — live store is SQLite; explain backup and corrupt-file conventions`

Stage:
- `README.md`

---

### Task 20: Final end-to-end verification

**Files:** none modified.

> Concept: belt-and-braces sweep before declaring M4 done. Build clean, all tests green, MVC separation grep still empty, and a manual once-over of the five console scenarios from the spec's verification checklist.

- [ ] **Step 1: Build clean**

Run: `dotnet build Kenaz.slnx`
Expected: 0 warnings, 0 errors.

- [ ] **Step 2: All tests pass**

Run: `dotnet test`
Expected: every test in the solution passes (~90 total).

- [ ] **Step 3: MVC separation grep is empty**

Run (from the repo root): `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'`
Expected: no matches. (`Kenaz.Core` continues to keep all IO inside `Storage/`.)

- [ ] **Step 4: Manual verification — five scenarios**

(All five from the spec's `## Verification (M4, end-to-end)` section. Use `%APPDATA%\Kenaz\` as the working folder and rename files aside between scenarios.)

1. **Migrating from existing M1–M3 JSON:** plant a `checkins.json` (no `.db`). Launch. Expect orientation line, `.db` appears, `checkins.backup-…json` appears, `checkins.json` gone. History shows all migrated check-ins. Run menu option 5 against the backup — should report `0 added / N unchanged`.
2. **Fresh install:** no files. Launch. No orientation line. Empty history.
3. **Subsequent launches:** files present from #2. Launch. No orientation line. Behaviour identical to a normal session.
4. **Both files present at startup:** with the `.db` in place from #3, also drop a `checkins.json` next to it. Launch. Expect orientation line (`CleanedUp` outcome), JSON disappears, a fresh `.backup-*.json` appears, DB contents untouched.
5. **Corrupt `checkins.db`:** write random bytes over the `.db` file. Launch. Expect `*.corrupt-*.bak` appears, a fresh empty `.db` takes its place, no crash, no orientation line. History is empty.

- [ ] **Step 5: Take a look at the repo state**

Spot-check `Kenaz.Core/Storage/`:
- `ICheckInRepository.cs` — unchanged.
- `CheckInDto.cs` — unchanged.
- `JsonCheckInRepository.cs` — only the doc comment is different from M3.
- `JsonCheckInArchive.cs` — unchanged.
- `SqliteCheckInRepository.cs` — new, ~150 lines.
- `JsonToSqliteMigrator.cs` — new, ~100 lines.
- `MigrationException.cs` — new, ~15 lines.
- `MigrationOutcome.cs` — new, ~15 lines.

- [ ] **Step 6: Update project memory**

After the manual verification passes and you're confident M4 is done, append a session-log entry to `~/.claude/projects/C--Users-Nugget-Documents-Development-GitHub-repos-kenaz/memory/MEMORY.md` summarising what shipped, the test count, and the next milestone (M5 — ASP.NET Minimal API over the data, loopback-only).

- [ ] **Step 7: Final M4 commit (only if anything is left unstaged after Tasks 1–19)**

If `git status` shows uncommitted changes that aren't covered by the per-task commits, group them into a single closing commit:

Commit message: `chore(M4): final cleanup before shipping`

Otherwise: nothing to commit — every task already had its own commit.

---

## Verification (M4, end-to-end)

Already executed inline in Task 20. The reference here is the spec's `## Verification (M4, end-to-end)` section ([2026-05-27-kenaz-m4-sqlite-design.md](../specs/2026-05-27-kenaz-m4-sqlite-design.md#verification-m4-end-to-end)) — keep that handy while running the five manual scenarios.
