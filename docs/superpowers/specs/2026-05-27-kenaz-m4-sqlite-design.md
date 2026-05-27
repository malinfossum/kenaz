# Kenaz M4 — Swap JSON → SQLite Behind the Repository

A storage swap, not a feature. The live backing store moves from a JSON file to a SQLite database; the journal, the views, the export format, the tests — all unchanged in behaviour. Existing data is migrated and verified through a crash-safe sentinel-path flow; a portable, importable JSON snapshot is written beside the new database as the backup.

---

## Context

M1 shipped the daily tool with JSON persistence. M2 formalized the repository seam (`ICheckInRepository`) and added portable JSON export/import beside it. M3 layered insight functions on top of the journal. Through all three milestones, `WellbeingJournal` has only ever known about one thing: `LoadAll()` / `SaveAll(list)`.

M4 is the storage swap promised by the original design ([2026-05-21-kenaz-design.md](2026-05-21-kenaz-design.md), milestone table line 83): *"Swap JSON → SQLite behind the same interface — migrating existing data (never dropped); JSON kept as a backup."* It is also the cash-out on Hardening invariant #2 (line 129): *"No data loss on storage swap (M4): migrate existing JSON into SQLite, verified, before retiring the JSON path; keep the JSON as a backup."*

The seam already exists. M2's `ICheckInRepository` doc comment said it out loud: *"the backing store (JSON now, SQLite later) can change behind it without touching the journal."* M4 collects on that promise.

**Intended outcome:** the user sees one warm orientation line on the migration run ("your check-ins have moved to a new file"), and from then on Kenaz looks and behaves exactly as before. Behind the scenes Kenaz now uses a real database; a portable JSON snapshot sits in `%APPDATA%\Kenaz\` as a dated, *importable* backup; her data has not moved through any intermediate format she has to trust.

---

## Concept

One new repository (`SqliteCheckInRepository`) and one one-shot migrator (`JsonToSqliteMigrator`). The journal is not touched. The interface is not touched. The export format is not touched.

The only files in `Kenaz.Core/` that gain knowledge of SQL are the two new ones in `Storage/`. Everything else stays exactly as M3 left it.

---

## Architecture

```
Kenaz.Core/Storage/
  ICheckInRepository.cs           (unchanged — already the right seam)
  CheckInDto.cs                   (unchanged)
  JsonCheckInRepository.cs        (kept; now a migration-source helper, not the live store)
  JsonCheckInArchive.cs           (unchanged — export/import is a separate concern)
  SqliteCheckInRepository.cs      (NEW — the live store)
  JsonToSqliteMigrator.cs         (NEW — one-shot, idempotent, runs on startup)
  MigrationException.cs           (NEW — narrow exception type for the migration boundary)
```

`JsonCheckInRepository` survives the swap because the migrator reads the legacy file through its `LoadAll()` — already corrupt-safe, already deduping, already re-validating every record through `CheckIn`'s constructor. Reusing it is cheaper than duplicating the recovery logic. The doc comment will be updated to say it is now a migration helper rather than the live store; the class itself does not change. Its tests stay.

**MVC separation holds.** `Kenaz.Core` continues to have zero `Console.`/`File.`/`Directory.`/`Path.` calls outside `Storage/`. The verification grep from M3 still passes.

---

## New domain (Core)

### `SqliteCheckInRepository`

Implements `ICheckInRepository` over a single SQLite file. Uses `Microsoft.Data.Sqlite` (lightweight ADO.NET provider — no EF, no DbContext, no migrations framework). The package is added to `Kenaz.Core.csproj`; `Kenaz.Tests.csproj` picks it up transitively via the existing project reference. `Kenaz.Tests.csproj` also gets `InternalsVisibleTo("Kenaz.Tests")` declared in `Kenaz.Core.csproj` so the migrator's `internal` test-seam overload is reachable.

```csharp
public sealed class SqliteCheckInRepository : ICheckInRepository
{
    public SqliteCheckInRepository(string filePath);

    public static string DefaultFilePath();   // %APPDATA%\Kenaz\checkins.db

    public IReadOnlyList<CheckIn> LoadAll();
    public void SaveAll(IReadOnlyList<CheckIn> checkIns);
}
```

**Constructor behaviour:** eagerly opens the connection, runs `CREATE TABLE IF NOT EXISTS …` (idempotent schema creation), then closes. No separate "initialize" step. If the open or the schema check throws `SqliteException` (the file exists but is not a readable SQLite database), the repository renames the bad file to `dbPath + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.bak"` (same `UtcNow`-inline pattern as `JsonCheckInRepository.BackUpCorruptFile`) and re-runs the open + schema creation against a fresh empty file — symmetric with `JsonCheckInRepository`'s corrupt-file recovery (invariant #1).

**`LoadAll()`:** single `SELECT ... ORDER BY Date`, maps each row into a `CheckIn` via its public constructor, drops rows that fail the constructor's validation (`ArgumentException`) — the same defence `JsonCheckInRepository` already practices. A `SqliteException` thrown during the read indicates the file became corrupt *after* construction (rare; e.g., external tampering between startup and first read). The same recovery path runs: back up, recreate, return empty list.

**`SaveAll(list)`:** wraps `DELETE FROM CheckIns` + a parameterized `INSERT` per row in a single transaction. Atomic by definition; if the transaction throws, the previous state is preserved. DELETE+INSERT was chosen over UPSERT because it matches the existing list-in / list-out contract literally — the journal's mental model is "here is the full list, persist it."

**SQL injection:** every value goes through named parameters (`$date`, `$mood`, …). No string concatenation of values into queries, anywhere.

**Locale safety — every text boundary pinned to `InvariantCulture`.** SQLite stores `decimal`, `DateOnly`, and `DateTimeOffset` as TEXT in this schema (precision + offset preservation), so write and read both cross a string boundary where the system locale could intervene. Norwegian default (`nb-NO`) formats decimals with comma (`7,5`); without explicit culture the round trip would silently misread. Concretely:

- *Writes:* `c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)`, `c.Sleep?.ToString(CultureInfo.InvariantCulture)`, `c.CreatedAt.ToString("o", CultureInfo.InvariantCulture)` (same for `UpdatedAt`). Each parameter is bound as `SqliteType.Text` with the pre-formatted string.
- *Reads:* `DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture)`, `decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture)`, `DateTimeOffset.ParseExact(reader.GetString(5), "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)`.

A test fixture sets `CultureInfo.CurrentCulture = new CultureInfo("nb-NO")` (and `CurrentUICulture` likewise) for the SQLite tests, then runs the full round trip, so the locale boundary is exercised cheaply.

### `JsonToSqliteMigrator`

```csharp
public static class JsonToSqliteMigrator
{
    public static MigrationOutcome MigrateIfNeeded(string jsonPath, string dbPath, DateTimeOffset now);

    // Test seam: same flow, but the verification step is replaceable. Production
    // callers go through the overload above, which passes the default verifier.
    internal static MigrationOutcome MigrateIfNeeded(
        string jsonPath,
        string dbPath,
        DateTimeOffset now,
        Func<IReadOnlyList<CheckIn>, IReadOnlyList<CheckIn>, bool> verify);
}

public enum MigrationOutcome { NoOp, FreshInstall, Migrated, CleanedUp }
```

Runs every startup. The flow is crash-safe by construction: at every moment between an interrupted run and the next launch, the data exists in at least one definitively-readable place.

**Pre-step (startup cleanup, runs every time before the main decision tree):**
- If `dbPath + ".migrating"` exists → orphan from an interrupted migration. `File.Delete` it. Continue into the main decision tree.
- If `dbPath` exists **and** `jsonPath` also exists → a previous migration crashed between promoting the new DB and writing the backup. SQLite is the live store; finish the cleanup now by reading `jsonPath`'s records via `JsonCheckInRepository.LoadAll()`, exporting them to `Path.Combine(Path.GetDirectoryName(dbPath)!, $"checkins.backup-{now:yyyyMMdd-HHmmss}.json")` via `JsonCheckInArchive.Export`, then `File.Delete(jsonPath)`. **Return `MigrationOutcome.CleanedUp` immediately — do not fall through to the main decision tree** (otherwise rule 1 would override the outcome with `NoOp` and the view would skip the orientation line).

**Main decision tree:**

1. **`dbPath` exists** → return immediately. SQLite is the live store; nothing to do.
2. **`dbPath` does NOT exist, `jsonPath` does NOT exist** → fresh install. Construct an empty `SqliteCheckInRepository` (which creates the schema). Return.
3. **`dbPath` does NOT exist, `jsonPath` does exist** → migrate via the sentinel-path pattern:
   1. `source = new JsonCheckInRepository(jsonPath).LoadAll()` — corrupt-safe by construction.
   2. `migratingPath = dbPath + ".migrating"` — sentinel; never a live store.
   3. `repo = new SqliteCheckInRepository(migratingPath); repo.SaveAll(source);` — schema + rows committed atomically inside `SaveAll`'s transaction. At no point does `dbPath` itself exist yet.
   4. `verifyList = repo.LoadAll();` — sort `source` and `verifyList` by date, then run `verify(source, verifyList)`. The default verifier asserts field-for-field equality on all 7 columns (date, mood, energy, sleep, note, CreatedAt, UpdatedAt). On `false`: `File.Delete(migratingPath)`, leave `jsonPath` untouched, throw `MigrationException`.
   5. `File.Move(migratingPath, dbPath)` — atomic promotion. The instant after this returns, SQLite is the live store.
   6. `new JsonCheckInArchive().Export(Path.Combine(Path.GetDirectoryName(dbPath)!, $"checkins.backup-{now:yyyyMMdd-HHmmss}.json"), source, now);` — write the backup in the same envelope format as a normal export, so menu option 5 (Import) can read it.
   7. `File.Delete(jsonPath)` — completes the migration.

   **Exception translation across step 3.3 → step 5 (inclusive):** any `SqliteException` (from `SaveAll`) or `IOException` (from `File.Move`) raised between starting step 3.3 and finishing step 5 is caught, `File.Delete`s `migratingPath` if it exists, and is re-thrown as a `MigrationException` (with the original exception as `InnerException`) so `Program.Main`'s warm error path takes over instead of the user seeing a raw stack trace.

   **Exception swallow across step 3.6 → step 3.7:** once step 5 has returned, the live store is committed and the user's data is safe. Any `IOException` thrown by the backup `Export` or the `File.Delete(jsonPath)` is caught, swallowed, and the migrator still returns `MigrationOutcome.Migrated`. The pre-step on the next launch (the "both files exist" branch) will retry the backup write and the JSON delete — the orientation line still fires on this run, and the cleanup completes on the next.

The verification step (3.4) is the cash-out on invariant #2's word *"verified"*. Count alone would miss field-level corruption (e.g., a future schema change that loses decimal precision). Full readback equality catches that.

**Crash recovery for every failure point:**

| Crashed between… | Files on disk | Next-launch behaviour |
|---|---|---|
| Loading `source` and writing `.migrating` | `jsonPath` exists, no `.migrating` | Decision tree case 3 retries from scratch. |
| Writing `.migrating` and verify | `jsonPath` exists, `.migrating` exists | Pre-step deletes `.migrating`; case 3 retries. |
| Verify and promote-to-dbPath | `jsonPath` exists, `.migrating` exists | Same as above; the (already-failed) verify will re-run. |
| Promote and writing backup *(I/O error caught, swallowed → `Migrated` returned, orientation line fires on this run)* | `jsonPath` exists, `dbPath` exists | Pre-step's "both files exist" branch on next launch writes the backup and deletes `jsonPath`. Returns `CleanedUp` (silent — orientation already shown). |
| Writing backup and delete `jsonPath` *(`File.Delete` I/O error caught, swallowed → `Migrated` returned)* | `jsonPath` exists, `dbPath` exists, plus a `checkins.backup-*.json` already on disk | Pre-step's "both files exist" branch writes a fresh backup (with the new timestamp) and deletes `jsonPath`. The earlier backup sits as harmless duplicate — same data, older timestamp. Returns `CleanedUp` (silent). |
| Hard process kill (no exception caught) at any point after step 5 | Whatever was on disk at the kill instant | Same as the two rows above — pre-step's "both files exist" branch handles either intermediate state idempotently. |

The `internal` overload is purely a test seam, exposed to `Kenaz.Tests` via `InternalsVisibleTo`. It lets `Verification_failure_*` exercise the rollback path without staging filesystem corruption, while production callers continue to use the public overload.

### `MigrationException`

A narrow custom exception thrown only by the migrator. `Program.Main` catches it, prints a warm error, and exits without constructing the journal — same shape as M2's `ImportException`.

---

## Schema

One table. Date is the primary key — enforces the one-per-date rule (invariant #4) at the DB level so the dedup loop the JSON repository runs at load time is no longer needed.

```sql
CREATE TABLE IF NOT EXISTS CheckIns (
    Date       TEXT    NOT NULL PRIMARY KEY,   -- ISO 'YYYY-MM-DD'
    Mood       INTEGER NULL,
    Energy     INTEGER NULL,
    Sleep      TEXT    NULL,                   -- decimal as string (precision)
    Note       TEXT    NULL,
    CreatedAt  TEXT    NOT NULL,               -- ISO 8601 round-trip ('o' format)
    UpdatedAt  TEXT    NOT NULL
);
```

**Type choices:**

| C# | SQLite | Why |
|---|---|---|
| `DateOnly` | `TEXT` (`yyyy-MM-dd`) | Human-readable if the DB is opened in a tool; SQLite has no native date type. |
| `int?` | `INTEGER NULL` | Direct mapping. NULL = skipped (matches the no-zero rule). |
| `decimal?` | `TEXT NULL` | `Microsoft.Data.Sqlite` default; preserves precision. `REAL` would risk rounding 7.5 ↔ 7.4999…. |
| `string?` | `TEXT NULL` | Trivial. |
| `DateTimeOffset` | `TEXT` (`o` round-trip) | Same library convention; round-trip format preserves the offset. |

**What I'm not adding:**
- No `_schema_version` table. M4 is `v1`; if M5/M6 needs versioning, add it with a real second version in hand. YAGNI.
- No indexes beyond the PK. With max ~3650 rows over 10 years, table scans are nothing.
- No `WAL` mode. Default rollback journal is fine for a single-user CLI; revisit if M5's API ever introduces concurrency.

---

## View (Console)

`Program.Main` gains: a wire-up of the migrator, a `try/catch` for `MigrationException`, and a one-line orientation message that fires only on the run that actually migrates. To know whether a migration ran, `MigrateIfNeeded` returns a small enum so the caller doesn't have to infer it from file mtimes:

```csharp
public enum MigrationOutcome
{
    NoOp,                // dbPath already existed at startup; nothing changed
    FreshInstall,        // no JSON, no DB — created an empty DB
    Migrated,            // legacy JSON found and migrated successfully
    CleanedUp,           // pre-step's "both files exist" branch finished a previous migration
}

public static MigrationOutcome MigrateIfNeeded(string jsonPath, string dbPath, DateTimeOffset now);
```

```csharp
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
// … everything from here is unchanged
```

No menu changes. No new options. The orientation line fires exactly on the first launch after migration (and on a recovery launch if the previous run crashed mid-promotion). On every other launch the user signs in to a check-in tool and sees a check-in tool.

---

## Decisions made (change either if you disagree)

| Decision | Choice | Why |
|---|---|---|
| Library | `Microsoft.Data.Sqlite` | One NuGet, raw SQL, manual mapping — mirrors how `CheckInDto` already maps JSON. Right scale for one table with ~7 columns. |
| Migration shape | Dedicated `JsonToSqliteMigrator`, invoked from `Program.Main` | Mirrors the M2 split (`JsonCheckInArchive` beside `JsonCheckInRepository`). Testable in isolation; controller stays in charge of wiring. |
| Migration durability | Sentinel-path (`dbPath + ".migrating"`) + atomic `File.Move` promotion, with startup cleanup of orphans and "both files exist" recovery | An interrupted migration must never leave the user with an empty live store while the JSON still has data — crash recovery is part of "no data loss" (invariant #2). |
| Verification | Full readback equality, field-for-field on all 7 columns | What invariant #2's "verified" actually means. Count alone misses field-level corruption. |
| Backup format | Written via `JsonCheckInArchive.Export` (versioned envelope), named `checkins.backup-YYYYMMDD-HHMMSS.json` | A backup the user can't restore is theatre. Writing in the export envelope makes menu option 5 (Import) work as a recovery path. Same data, lossless, importable. |
| Original-file fate | Original `checkins.json` is `File.Delete`d after the export-format backup is written | The data is preserved (and restorable) through the export-format backup; keeping the raw legacy shape adds no value and confuses the folder. |
| `JsonCheckInRepository` fate | Kept; reused by the migrator as a corrupt-safe reader | Cheaper than duplicating its corrupt-file recovery + dedup + re-validation logic. |
| `SaveAll` strategy | `DELETE FROM CheckIns` + parameterized `INSERT`s in one transaction | Literal translation of the list-in / list-out contract. Atomic. Easy to reason about at this scale. |
| Primary key | `Date` | Enforces invariant #4 at the DB level; lets `LoadAll` skip the dedup loop the JSON repo needed. |
| Date storage | `TEXT` (ISO `yyyy-MM-dd`) | Human-readable on inspection; SQLite has no native date type anyway. |
| Decimal storage | `TEXT`, formatted/parsed with `InvariantCulture` | Preserves precision; `REAL` risks rounding errors on values like 7.5. Invariant culture avoids `nb-NO` comma-decimal misreads on the string boundary. |
| Timestamp storage | `TEXT` ISO 8601 round-trip (`"o"` format with `InvariantCulture`) | Preserves the offset; explicit `ParseExact("o", …, RoundtripKind)` on read for locale safety. |
| SQLite corrupt-file recovery | `SqliteException` on open/read → back up as `*.corrupt-*.bak`, fall back to empty schema | Symmetric with `JsonCheckInRepository`; carries invariant #1 across the storage swap. |
| DB location | `%APPDATA%\Kenaz\checkins.db` (next to the legacy JSON) | Same folder convention as M1; predictable. |
| Schema versioning | Not added in M4 | YAGNI; add when there's a real second version. |
| Journal mode | SQLite default (rollback journal) | Single-user CLI; WAL is overkill. |
| Console orientation message | One warm line on migration runs only | The data file is changing under the user; orienting her once is worth more than perfect silence. Suppressed on every other launch. |

---

## Out of scope (explicit cuts)

- **Interface evolution.** No `Upsert(checkIn)` / `Delete(date)` methods on `ICheckInRepository`. M5 (API) is a more natural moment for that, with concrete needs in hand.
- **Schema versioning / migration framework.** Not needed for one table on one version. YAGNI.
- **Encryption at rest.** Plaintext-at-rest is the accepted trade-off through M1–M5 (invariant #9). Optional encryption is M6 territory.
- **Console error handling for SQLite errors outside migration.** M1/M2/M3 surface storage errors without top-level wrapping; M4 keeps that consistent. A general hardening pass is a separate piece of work if it's ever wanted.
- **Performance work.** No connection pooling beyond what `Microsoft.Data.Sqlite` already does, no batching beyond the single-transaction `SaveAll`. Data volume doesn't warrant it.
- **A "migrate back to JSON" path.** Out: the export side already produces portable JSON (`JsonCheckInArchive`), which is the right primitive if the user ever wants their data outside SQLite.
- **Console tests.** Matches M1/M2/M3 convention — `Program.cs` is verified by running.

---

## Testing

All new tests follow the existing `JsonCheckInRepositoryTests` fixture pattern (per-test temp folder via `Guid.NewGuid().ToString("N")`, `TearDown` cleans it up). **~24 new tests, bringing total to ~90.**

### `SqliteCheckInRepositoryTests` (~11 tests)
- `LoadAll_returns_empty_when_db_is_new`
- `SaveAll_then_LoadAll_round_trips_all_fields_including_timestamps`
- `SaveAll_then_LoadAll_preserves_decimal_precision` (7.5 ↔ 7.5, never 7.4999…)
- `SaveAll_then_LoadAll_preserves_null_fields` (skipped scales stay null, never 0)
- `SaveAll_replaces_previous_state` (second call wins; no leftover rows from the first)
- `SaveAll_empty_list_clears_the_table`
- `SaveAll_with_duplicate_dates_in_input_rolls_back_atomically` (PK conflict → transaction rolls back, store unchanged from before the call)
- `Schema_is_created_on_first_open` (constructor on missing file produces a queryable table)
- `DefaultFilePath_ends_with_Kenaz_checkins_db`
- `Constructor_on_corrupt_db_backs_it_up_and_starts_fresh` (writes random bytes to `dbPath`, constructs the repo, asserts: original file renamed to `*.corrupt-*.bak`, new empty DB present, `LoadAll` returns empty)
- `Round_trip_under_nb_NO_locale_preserves_decimal_and_timestamp` (fixture-level `CultureInfo.CurrentCulture = nb-NO`; full round trip on a check-in with `Sleep = 7.5m` and a non-UTC offset)

### `JsonToSqliteMigratorTests` (~13 tests)
- `Skips_when_db_already_exists_returns_NoOp` (marker .db present; JSON and .db both untouched after the call; outcome = `NoOp`)
- `Fresh_install_creates_empty_db_and_no_backup_returns_FreshInstall` (neither file exists → empty .db, no `.backup-*`; outcome = `FreshInstall`)
- `Migrates_records_from_legacy_json_into_db_returns_Migrated` (write JSON, run migrator, .db has the rows; outcome = `Migrated`)
- `Writes_export_format_backup_after_successful_migration` (the file `checkins.backup-YYYYMMDD-HHMMSS.json` exists, deserializes as `ExportDocumentDto`, contains the same records as the source)
- `Deletes_original_json_after_successful_migration` (`jsonPath` no longer exists post-migration)
- `Backup_is_importable_via_archive` (after migration, `JsonCheckInArchive.Import` on the backup file returns the same records — proves recoverability)
- `Verification_failure_deletes_migrating_path_and_keeps_json` (uses the migrator's `internal` overload with a stub verifier that returns `false`; asserts: `MigrationException` thrown, `dbPath` doesn't exist, `dbPath + ".migrating"` doesn't exist, `jsonPath` still exists at the original location, no `.backup-*` produced)
- `Corrupt_legacy_json_results_in_empty_db_and_corrupt_backup` (already-corrupt JSON → empty .db, original JSON gets `*.corrupt-*.bak` from the JSON repo's recovery, no `MigrationException` because verification of an empty source against an empty store passes; outcome = `Migrated`)
- `Migrated_db_round_trips_with_journal` (write JSON with a few days, run migrator, `WellbeingJournal` over the new SQLite returns the same data — covers the integration seam)
- `Orphan_migrating_file_is_cleaned_up_then_migration_proceeds` (pre-existing `.migrating` file + a `jsonPath` → pre-step deletes the orphan, main flow migrates, outcome = `Migrated`)
- `Both_dbPath_and_jsonPath_exist_triggers_cleanup_returns_CleanedUp` (pre-stage a populated `dbPath` + a `jsonPath` with different content → outcome = `CleanedUp`, `jsonPath` deleted, a `checkins.backup-*.json` containing `jsonPath`'s contents is present, `dbPath`'s contents untouched)
- `Both_files_present_with_unreadable_json_still_completes_cleanup` (`dbPath` exists, `jsonPath` is corrupt → JSON repo's recovery backs up the corrupt JSON, cleanup writes a backup containing zero records, `jsonPath` deleted, no exception thrown)
- `Cleanup_backup_uses_now_timestamp_when_an_earlier_backup_exists` (pre-stage an existing `checkins.backup-OLDER.json` → cleanup writes a second backup at the `now` timestamp, both files coexist as harmless duplicates)

---

## Verification (M4, end-to-end)

- `dotnet build` — clean (0 warnings / 0 errors).
- `dotnet test` — all NUnit tests pass (~90 total).
- `dotnet run --project Kenaz.Console` — manual:
  1. **With existing `checkins.json` from M1–M3:** first launch prints the orientation line, `checkins.db` appears, `checkins.backup-YYYYMMDD-HHMMSS.json` appears (in export envelope format), original `checkins.json` is gone, all data visible through option 3 (history) and option 6 (weekly review). Run menu option 5 (Import) against the backup file → reports 0 added / N unchanged (round-trip proof).
  2. **Fresh install (no JSON):** first launch creates an empty `checkins.db`, no `.backup-*`, no orientation line, options 2/3/6 show their empty-state copy.
  3. **Subsequent launches:** no migration runs (verify by file mtimes); no orientation line; behaviour identical to a normal session.
  4. **Both files present at startup** (manually staged: copy a saved JSON next to an active DB): launch prints the orientation line (`CleanedUp` outcome), JSON disappears, a fresh `.backup-*.json` appears, DB contents untouched.
  5. **Corrupt `checkins.db`** (manually staged: write random bytes to the DB file): launch backs up the bad file as `*.corrupt-*.bak` and starts with an empty store; warm message implicit (the user just sees an empty history). No crash.
- **MVC separation check:** `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` — expected: no matches. (M4 adds zero IO to Core outside `Storage/`.)

---

## Self-review (spec coverage vs original milestone line 83)

- *Swap JSON → SQLite behind the same interface* → `SqliteCheckInRepository` implements `ICheckInRepository`. The journal is not touched.
- *Migrating existing data (never dropped)* → `JsonToSqliteMigrator` runs through a sentinel-path flow that keeps the data readable at every step; pre-step recovery handles any interrupted previous run.
- *Verified* → field-for-field readback equality before the migrating-path DB is promoted to live (invariant #2).
- *JSON kept as a backup* → `checkins.backup-YYYYMMDD-HHMMSS.json` after successful migration, in the same versioned envelope as a normal export, so menu option 5 can restore from it. The data is preserved (no loss); the literal raw file is deleted because the export-format backup is both a faithful copy *and* a recoverable one.

---

## How to proceed (after approval)

1. Hand off to **writing-plans** to produce `docs/superpowers/plans/2026-05-27-kenaz-m4-sqlite.md` with TDD task-by-task steps, carrying the migrator's invariants (verification, sentinel-path durability, crash-recovery branches, idempotency) and the SQLite corrupt-file recovery (invariant #1 symmetry) as explicit acceptance criteria.
2. Execute task-by-task: TDD, one task = one commit, commits via GitHub Desktop (no `Co-Authored-By`).
3. **Learning mode applies** (per the original spec): each task carries a short `> Concept:` note on the new C# / SQLite idea before the code (e.g. parameterized commands, transactions, `IDataReader`, `using` on the connection, `InternalsVisibleTo`, `InvariantCulture` on string-bridge types).
4. **README update** is part of the plan, not a separate task list:
   - Note that the live store is now SQLite (`%APPDATA%\Kenaz\checkins.db`).
   - One bullet explaining the `checkins.backup-*.json` file(s): written by the migration in the same format as a normal export. You may occasionally see more than one if a previous migration was interrupted — they contain the same historic data (the timestamp in the filename tells you which is which), and any of them is importable via menu option 5 if recovery is ever needed. Plaintext (same caveat as exports); safe to delete once you've confirmed your check-ins are intact in the new store (option 3 or 6).
   - One bullet acknowledging the corrupt-file recovery behaviour (`*.corrupt-*.bak`) for both stores, so a user who sees one of those files in the folder knows what it is.
