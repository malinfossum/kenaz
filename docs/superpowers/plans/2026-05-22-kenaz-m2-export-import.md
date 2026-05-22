# Kenaz M2 — Export / Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user export all their check-ins to a portable, versioned JSON file and import one back safely — without losing or corrupting existing data.

**Architecture:** A new `JsonCheckInArchive` (Kenaz.Core/Storage) owns export/import file IO — the only new place in Core that touches disk, sitting *beside* `ICheckInRepository` so the live-store seam stays list-in / list-out (clean for the M4 SQLite swap). Export wraps check-ins in a versioned envelope and writes atomically (temp + move). Import parses, re-validates each record by constructing a real `CheckIn` (dropping malformed ones), and hands the survivors to a new `WellbeingJournal.Merge` that applies them newer-wins. The console gets two menu options that wire it together with warm copy and gentle error handling.

**Tech Stack:** C# / net10.0, System.Text.Json, NUnit 4.x. No new NuGet packages.

---

## Context

M1 shipped a usable daily tool (check-in, history, 7-day view, streak) persisted to `%APPDATA%\Kenaz\checkins.json`. M2 is the **data-portability + resilience** layer from the spec ([docs/superpowers/specs/2026-05-21-kenaz-design.md](../specs/2026-05-21-kenaz-design.md), milestone table line 81): *"Repository interface formalized + JSON export/import (validated import; plaintext-export warning) + error handling."*

Why it matters: the user's data should be hers to take (privacy + portability), and import must be hardened against untrusted files (Hardening invariant 6) before M4 swaps JSON → SQLite. Export is unencrypted-at-rest by accepted trade-off (invariant 9), so it carries a plaintext warning.

**Learning mode applies** (spec line 146): each task carries a short `> Concept:` note on the new C# idea before the code.

### Decisions made (change either if you disagree)

1. **Export destination — auto-save, no path typing.** Export writes to `Documents\Kenaz\kenaz-backup-YYYYMMDD-HHmmss.json` and prints the full path. *Why:* lowest friction, discoverable (not hidden AppData), never clobbers an earlier backup (timestamped). Import still asks which file to bring in — you must choose that one.
2. **Import conflict policy — newer-wins.** When an imported date already exists locally, the entry with the later `UpdatedAt` is kept. *Why:* no silent data loss — restoring an old backup won't wipe newer edits, and re-importing a fresher file does update. Deterministic and testable. (Alternatives were file-always-wins or additive-only.)

### Out of scope for M2

XSS escaping on render and the JS `__proto__` prototype-pollution guard (Hardening invariant 6) are **M6 render-boundary** concerns — there's no HTML render here. App-lock / encryption-at-rest are later opt-in (invariant 9). Importing the raw `checkins.json` (bare array, no envelope) is not supported — import expects the export envelope.

---

## File Structure

**Create:**
- `Kenaz.Core/Storage/ExportDocumentDto.cs` — internal serialization envelope: `SchemaVersion`, `ExportedAt`, `CheckIns`.
- `Kenaz.Core/Storage/JsonCheckInArchive.cs` — public; `Export`, `Import`, `DefaultExportPath`, `CurrentSchemaVersion`. Plus `ImportResult` and `ImportException` (same file, focused types).
- `Kenaz.Core/Services/MergeResult.cs` — public result of a merge (`Added`, `Updated`, `Unchanged`).
- `Kenaz.Tests/JsonCheckInArchiveTests.cs` — export/import behaviour via temp files.

**Modify:**
- `Kenaz.Core/Services/WellbeingJournal.cs` — add `Merge(IReadOnlyList<CheckIn>)`.
- `Kenaz.Core/Storage/ICheckInRepository.cs` — doc-comment only: clarify the seam stays list-in/out (the "formalized" interface).
- `Kenaz.Tests/WellbeingJournalTests.cs` — add merge tests.
- `Kenaz.Console/Program.cs` — two menu options + `ExportCheckIns` / `ImportCheckIns`.
- `README.md` — document export/import + plaintext note.

**Reuse (do not duplicate the ideas, follow the patterns):** atomic write (temp + `File.Move(overwrite)`) and per-record re-validation from [JsonCheckInRepository.cs](../../../Kenaz.Core/Storage/JsonCheckInRepository.cs); `CheckInDto` shape; `DefaultFilePath()` style; the temp-dir `[SetUp]/[TearDown]` and fixed-clock test patterns from `JsonCheckInRepositoryTests` / `WellbeingJournalTests`; `InMemoryCheckInRepository` test double.

> **Commits:** one task = one commit, made in **GitHub Desktop** (no `Co-Authored-By`). Push is Desktop-only and your call. Commit messages below are suggestions.

---

### Task 1: Versioned export

**Files:**
- Create: `Kenaz.Core/Storage/ExportDocumentDto.cs`
- Create: `Kenaz.Core/Storage/JsonCheckInArchive.cs`
- Test: `Kenaz.Tests/JsonCheckInArchiveTests.cs`

> Concept: an export needs a **version stamp** so a future import can recognise old files and tolerate format changes. We wrap the check-ins in an *envelope* (`{ schemaVersion, exportedAt, checkIns }`) instead of writing a bare array. Same atomic-write trick as the repository: write a `.tmp`, then move over the target so a crash never leaves a half file.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Kenaz.Tests;

public class JsonCheckInArchiveTests
{
    private string _dir = null!;
    private string _path = null!;

    private static readonly DateOnly Day = new DateOnly(2026, 5, 22);
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-archive-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "export.json");
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
    public void Export_writes_a_versioned_envelope()
    {
        var checkIn = new CheckIn(Day, mood: 7, energy: 6, sleep: 7.5m, note: "ok day", createdAt: Created, updatedAt: Updated);

        new JsonCheckInArchive().Export(_path, new[] { checkIn }, exportedAt: Updated);

        Assert.That(File.Exists(_path), Is.True);
        var json = File.ReadAllText(_path);
        Assert.That(json, Does.Contain("\"SchemaVersion\": 1"));
        Assert.That(json, Does.Contain("\"Mood\": 7"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~JsonCheckInArchiveTests"`
Expected: FAIL — `JsonCheckInArchive` / `Export` does not exist (compile error).

- [ ] **Step 3: Write the envelope DTO**

`Kenaz.Core/Storage/ExportDocumentDto.cs`:

```csharp
namespace Kenaz.Core;

/// <summary>
/// Serialization shape for an export file: a versioned envelope around the check-ins.
/// The version lets a future import recognise and tolerate older files.
/// </summary>
internal sealed class ExportDocumentDto
{
    public int SchemaVersion { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
    public List<CheckInDto> CheckIns { get; set; } = new();
}
```

- [ ] **Step 4: Write the archive with Export**

`Kenaz.Core/Storage/JsonCheckInArchive.cs`:

```csharp
using System.Text.Json;

namespace Kenaz.Core;

/// <summary>
/// Reads and writes a portable, versioned JSON export of all check-ins. Separate from
/// <see cref="ICheckInRepository"/> (the live store) so that seam stays list-in / list-out.
/// Writes are atomic (temp file + move); imports are validated record-by-record.
/// </summary>
public class JsonCheckInArchive
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

    public static string DefaultExportPath(DateTimeOffset exportedAt)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var fileName = $"kenaz-backup-{exportedAt:yyyyMMdd-HHmmss}.json";
        return Path.Combine(documents, "Kenaz", fileName);
    }

    public void Export(string path, IReadOnlyList<CheckIn> checkIns, DateTimeOffset exportedAt)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new ExportDocumentDto
        {
            SchemaVersion = CurrentSchemaVersion,
            ExportedAt = exportedAt,
            CheckIns = checkIns.Select(ToDto).ToList(),
        };

        var json = JsonSerializer.Serialize(document, Options);

        var tempPath = path + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private static CheckInDto ToDto(CheckIn checkIn)
    {
        return new CheckInDto
        {
            Date = checkIn.Date,
            Mood = checkIn.Mood,
            Energy = checkIn.Energy,
            Sleep = checkIn.Sleep,
            Note = checkIn.Note,
            CreatedAt = checkIn.CreatedAt,
            UpdatedAt = checkIn.UpdatedAt,
        };
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~JsonCheckInArchiveTests"`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

GitHub Desktop — message: `feat: add versioned JSON export for check-ins`

---

### Task 2: Validated import

**Files:**
- Modify: `Kenaz.Core/Storage/JsonCheckInArchive.cs` (add `Import`, `ImportResult`, `ImportException`)
- Test: `Kenaz.Tests/JsonCheckInArchiveTests.cs`

> Concept: an import file is **untrusted**. We never trust the JSON directly — we rebuild each record by constructing a real `CheckIn`, and any record that breaks the invariants (e.g. `Mood = 99`) is dropped and counted, exactly like the repository's recoverable load. Unreadable files (missing, not JSON, newer schema) are wrapped into one `ImportException` so the console has a single, friendly failure to catch.

- [ ] **Step 1: Write the failing tests**

Add to `JsonCheckInArchiveTests`:

```csharp
[Test]
public void Export_then_import_round_trips_with_timestamps()
{
    var checkIn = new CheckIn(Day, mood: 7, energy: 6, sleep: 7.5m, note: "ok day", createdAt: Created, updatedAt: Updated);
    var archive = new JsonCheckInArchive();

    archive.Export(_path, new[] { checkIn }, exportedAt: Updated);
    var result = archive.Import(_path);

    Assert.That(result.Records, Has.Count.EqualTo(1));
    Assert.That(result.Skipped, Is.EqualTo(0));
    var loaded = result.Records[0];
    Assert.That(loaded.Date, Is.EqualTo(Day));
    Assert.That(loaded.Mood, Is.EqualTo(7));
    Assert.That(loaded.CreatedAt, Is.EqualTo(Created));
    Assert.That(loaded.UpdatedAt, Is.EqualTo(Updated));
}

[Test]
public void Import_drops_records_that_break_invariants_and_counts_them()
{
    var json = """
    {
      "SchemaVersion": 1,
      "ExportedAt": "2026-05-22T20:00:00+00:00",
      "CheckIns": [
        { "Date": "2026-05-22", "Mood": 7, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-22T08:00:00+00:00", "UpdatedAt": "2026-05-22T20:00:00+00:00" },
        { "Date": "2026-05-21", "Mood": 99, "Energy": null, "Sleep": null, "Note": null, "CreatedAt": "2026-05-21T08:00:00+00:00", "UpdatedAt": "2026-05-21T20:00:00+00:00" }
      ]
    }
    """;
    File.WriteAllText(_path, json);

    var result = new JsonCheckInArchive().Import(_path);

    Assert.That(result.Records, Has.Count.EqualTo(1));
    Assert.That(result.Skipped, Is.EqualTo(1));
}

[Test]
public void Import_missing_file_throws_ImportException()
{
    Assert.Throws<ImportException>(() => new JsonCheckInArchive().Import(_path));
}

[Test]
public void Import_unreadable_file_throws_ImportException()
{
    File.WriteAllText(_path, "this is not json");

    Assert.Throws<ImportException>(() => new JsonCheckInArchive().Import(_path));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~JsonCheckInArchiveTests"`
Expected: FAIL — `Import`, `ImportResult`, `ImportException` do not exist.

- [ ] **Step 3: Add ImportResult and ImportException**

Append to `Kenaz.Core/Storage/JsonCheckInArchive.cs` (below the `JsonCheckInArchive` class, same file/namespace):

```csharp
/// <summary>The outcome of an import: the records that survived validation, and how many were dropped.</summary>
public sealed class ImportResult
{
    public ImportResult(IReadOnlyList<CheckIn> records, int skipped)
    {
        Records = records;
        Skipped = skipped;
    }

    public IReadOnlyList<CheckIn> Records { get; }
    public int Skipped { get; }
}

/// <summary>Raised when an export file can't be read at all (missing, not JSON, or a newer schema).</summary>
public sealed class ImportException : Exception
{
    public ImportException(string message) : base(message)
    {
    }
}
```

- [ ] **Step 4: Add the Import method**

Add inside the `JsonCheckInArchive` class, after `Export`:

```csharp
public ImportResult Import(string path)
{
    if (!File.Exists(path))
    {
        throw new ImportException("I couldn't find a file at that path.");
    }

    ExportDocumentDto? document;
    try
    {
        var json = File.ReadAllText(path);
        document = JsonSerializer.Deserialize<ExportDocumentDto>(json, Options);
    }
    catch (JsonException)
    {
        throw new ImportException("That file isn't a readable Kenaz export.");
    }

    if (document is null)
    {
        throw new ImportException("That file isn't a readable Kenaz export.");
    }

    if (document.SchemaVersion > CurrentSchemaVersion)
    {
        throw new ImportException("That export was made by a newer version of Kenaz.");
    }

    var records = new List<CheckIn>();
    var seenDates = new HashSet<DateOnly>();
    var skipped = 0;

    foreach (var dto in document.CheckIns)
    {
        if (seenDates.Contains(dto.Date))
        {
            continue;
        }

        try
        {
            var checkIn = new CheckIn(dto.Date, dto.Mood, dto.Energy, dto.Sleep, dto.Note, dto.CreatedAt, dto.UpdatedAt);
            records.Add(checkIn);
            seenDates.Add(dto.Date);
        }
        catch (ArgumentException)
        {
            skipped++;
        }
    }

    return new ImportResult(records, skipped);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~JsonCheckInArchiveTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

GitHub Desktop — message: `feat: add validated JSON import with recoverable parsing`

---

### Task 3: Newer-wins merge

**Files:**
- Create: `Kenaz.Core/Services/MergeResult.cs`
- Modify: `Kenaz.Core/Services/WellbeingJournal.cs` (add `Merge`)
- Test: `Kenaz.Tests/WellbeingJournalTests.cs`

> Concept: import must **not** go through `AddOrUpdate` — that stamps a fresh `UpdatedAt` from the clock and would erase the imported record's real timestamp. Instead `Merge` keeps each side's original timestamps and decides per date: new date → add; same date, incoming newer → replace; otherwise keep what's there. That's why import reconstructs full `CheckIn` objects (with `CreatedAt`/`UpdatedAt`) in Task 2.

- [ ] **Step 1: Write the failing tests**

Add to `WellbeingJournalTests` (use the class's existing fixed-clock convention; the locals below are self-contained):

```csharp
[Test]
public void Merge_adds_check_ins_for_new_dates()
{
    var now = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.Zero);
    var journal = new WellbeingJournal(new InMemoryCheckInRepository(), () => now);
    var day = new DateOnly(2026, 5, 20);
    var incoming = new[] { new CheckIn(day, 5, null, null, null, createdAt: now, updatedAt: now) };

    var result = journal.Merge(incoming);

    Assert.That(result.Added, Is.EqualTo(1));
    Assert.That(journal.GetByDate(day), Is.Not.Null);
}

[Test]
public void Merge_updates_when_incoming_is_newer()
{
    var now = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.Zero);
    var journal = new WellbeingJournal(new InMemoryCheckInRepository(), () => now);
    var day = new DateOnly(2026, 5, 22);
    journal.AddOrUpdate(day, 3, null, null, null); // existing UpdatedAt = now
    var newer = new CheckIn(day, 9, null, null, null, createdAt: now, updatedAt: now.AddHours(1));

    var result = journal.Merge(new[] { newer });

    Assert.That(result.Updated, Is.EqualTo(1));
    Assert.That(journal.GetByDate(day)!.Mood, Is.EqualTo(9));
}

[Test]
public void Merge_keeps_existing_when_incoming_is_older()
{
    var now = new DateTimeOffset(2026, 5, 22, 20, 0, 0, TimeSpan.Zero);
    var journal = new WellbeingJournal(new InMemoryCheckInRepository(), () => now);
    var day = new DateOnly(2026, 5, 22);
    journal.AddOrUpdate(day, 3, null, null, null); // existing UpdatedAt = now
    var older = new CheckIn(day, 9, null, null, null, createdAt: now, updatedAt: now.AddHours(-1));

    var result = journal.Merge(new[] { older });

    Assert.That(result.Unchanged, Is.EqualTo(1));
    Assert.That(journal.GetByDate(day)!.Mood, Is.EqualTo(3));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~WellbeingJournalTests.Merge"`
Expected: FAIL — `Merge` / `MergeResult` do not exist.

- [ ] **Step 3: Add MergeResult**

`Kenaz.Core/Services/MergeResult.cs`:

```csharp
namespace Kenaz.Core;

/// <summary>How a merge resolved: dates newly added, existing dates replaced by a newer record, and dates left as they were.</summary>
public sealed class MergeResult
{
    public MergeResult(int added, int updated, int unchanged)
    {
        Added = added;
        Updated = updated;
        Unchanged = unchanged;
    }

    public int Added { get; }
    public int Updated { get; }
    public int Unchanged { get; }
}
```

- [ ] **Step 4: Add the Merge method**

Add to `WellbeingJournal` (after `AddOrUpdate`):

```csharp
/// <summary>
/// Folds imported check-ins into the store, keyed by date. A new date is added; an existing date is
/// replaced only when the incoming record is more recently updated; otherwise it's left unchanged.
/// Imported timestamps are preserved (this never routes through <see cref="AddOrUpdate"/>).
/// </summary>
public MergeResult Merge(IReadOnlyList<CheckIn> incoming)
{
    var byDate = _repository.LoadAll().ToDictionary(c => c.Date);

    var added = 0;
    var updated = 0;
    var unchanged = 0;

    foreach (var candidate in incoming)
    {
        if (!byDate.TryGetValue(candidate.Date, out var existing))
        {
            byDate[candidate.Date] = candidate;
            added++;
        }
        else if (candidate.UpdatedAt > existing.UpdatedAt)
        {
            byDate[candidate.Date] = candidate;
            updated++;
        }
        else
        {
            unchanged++;
        }
    }

    _repository.SaveAll(byDate.Values.ToList());
    return new MergeResult(added, updated, unchanged);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~WellbeingJournalTests.Merge"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

GitHub Desktop — message: `feat: add newer-wins merge for imported check-ins`

---

### Task 4: Console export option

**Files:**
- Modify: `Kenaz.Console/Program.cs`

> Concept: the console is View + Controller — it picks *where* the file goes (default path) and speaks to the user; Core does the writing. Per the spec the export carries a plaintext warning, and any IO failure should be a calm message, never a crash. (No unit test — `Program.cs` isn't unit-tested in M1; the logic it calls is already covered. Verify by running.)

- [ ] **Step 1: Add the menu line**

In `ShowMenu`, insert before the `0) Exit` line:

```csharp
        WriteLine("  4) Export your check-ins");
```

- [ ] **Step 2: Add the switch case and fix the fallback hint**

In `Main`'s `switch`, add before `case "0":`:

```csharp
                case "4":
                    ExportCheckIns(journal);
                    break;
```

And update the `default` line to:

```csharp
                    WriteLine("I didn't catch that — please choose 1, 2, 3, 4, or 0.");
```

- [ ] **Step 3: Add the ExportCheckIns method**

Add a new `private static` method (e.g. after `ShowHistory`):

```csharp
private static void ExportCheckIns(WellbeingJournal journal)
{
    var checkIns = journal.History();

    WriteLine();
    if (checkIns.Count == 0)
    {
        WriteLine("There's nothing to export yet — check in first, then your data is yours to take.");
        return;
    }

    var now = DateTimeOffset.Now;
    var path = JsonCheckInArchive.DefaultExportPath(now);

    WriteLine("Heads up: the export file is unencrypted — keep it somewhere private.");

    try
    {
        new JsonCheckInArchive().Export(path, checkIns, now);
        WriteLine($"Saved {checkIns.Count} check-in(s) to:");
        WriteLine($"  {path}");
    }
    catch (IOException)
    {
        WriteLine("I couldn't write the file just now — check the folder and try again.");
    }
}
```

- [ ] **Step 4: Build and verify by running**

Run: `dotnet build` — Expected: 0 warnings / 0 errors.
Run: `dotnet run --project Kenaz.Console`, choose `4`. Expected: the warning prints, then a path under `Documents\Kenaz\kenaz-backup-*.json`. Open the file — it's a `{ "SchemaVersion": 1, ... }` envelope with your check-ins.

- [ ] **Step 5: Commit**

GitHub Desktop — message: `feat: add export option to the console menu`

---

### Task 5: Console import option

**Files:**
- Modify: `Kenaz.Console/Program.cs`

> Concept: import is the one place the user picks the file, so we prompt for a path. Storage failures arrive as `ImportException` (one catch, friendly message); record-level problems arrive as a `Skipped` count. We report the merge outcome in plain, non-alarming language.

- [ ] **Step 1: Add the menu line**

In `ShowMenu`, insert after the `4) Export...` line and before `0) Exit`:

```csharp
        WriteLine("  5) Import check-ins");
```

- [ ] **Step 2: Add the switch case and fix the fallback hint**

In `Main`'s `switch`, add before `case "0":`:

```csharp
                case "5":
                    ImportCheckIns(journal);
                    break;
```

And update the `default` line to:

```csharp
                    WriteLine("I didn't catch that — please choose 1, 2, 3, 4, 5, or 0.");
```

- [ ] **Step 3: Add the ImportCheckIns method**

Add a new `private static` method (e.g. after `ExportCheckIns`):

```csharp
private static void ImportCheckIns(WellbeingJournal journal)
{
    WriteLine();
    Write("Path to the export file you want to bring in: ");
    var path = ReadLine();
    if (string.IsNullOrWhiteSpace(path))
    {
        WriteLine("No path given — nothing imported.");
        return;
    }

    ImportResult result;
    try
    {
        result = new JsonCheckInArchive().Import(path.Trim());
    }
    catch (ImportException ex)
    {
        WriteLine(ex.Message);
        return;
    }

    var merge = journal.Merge(result.Records);

    WriteLine($"Brought in {merge.Added} new day(s), updated {merge.Updated}, left {merge.Unchanged} unchanged.");
    if (result.Skipped > 0)
    {
        WriteLine($"Skipped {result.Skipped} entry(ies) I couldn't read.");
    }
}
```

- [ ] **Step 4: Build and verify by running**

Run: `dotnet build` — Expected: 0 warnings / 0 errors.
Run: `dotnet run --project Kenaz.Console`. Verify end-to-end:
1. Export (option 4), note the path.
2. Edit today's check-in (option 1) to a new mood.
3. Import (option 5) that file → today shows **unchanged** (your live edit is newer), older days **brought in**; the summary prints.
4. Import a made-up path → friendly "couldn't find a file" message, no crash.

- [ ] **Step 5: Commit**

GitHub Desktop — message: `feat: add import option to the console menu`

---

### Task 6: Docs + interface seam

**Files:**
- Modify: `Kenaz.Core/Storage/ICheckInRepository.cs` (doc comment only)
- Modify: `README.md`

> Concept: "Repository interface formalized" means the live-store contract stays exactly `LoadAll` / `SaveAll` — export/import live *beside* it in `JsonCheckInArchive`, so the M4 SQLite swap still touches only one implementation. We just make that explicit in the doc comment; no behaviour change.

- [ ] **Step 1: Clarify the interface doc comment**

In `ICheckInRepository.cs`, replace the `<summary>` with:

```csharp
/// <summary>
/// Storage contract for the live check-in store: list-in / list-out, nothing more. The backing
/// store (JSON now, SQLite later) can change behind it without touching the journal. Portability —
/// export/import to a file — is a separate concern in <see cref="JsonCheckInArchive"/>, deliberately
/// kept off this seam so swapping the store stays a one-class change.
/// </summary>
```

- [ ] **Step 2: Document export/import in the README**

Add a short section to `README.md` (English, lean, no marketing — match the existing tone). Suggested content:

```markdown
## Export and import

From the menu, choose **Export** to save all your check-ins to
`Documents\Kenaz\kenaz-backup-<timestamp>.json`, or **Import** to bring a backup
back in. Imported days merge by date — the more recently edited entry wins, so a
restore never overwrites newer changes.

The export file is plain, unencrypted JSON — keep it somewhere private.
```

- [ ] **Step 3: Build to confirm nothing broke**

Run: `dotnet build` — Expected: 0 warnings / 0 errors.

- [ ] **Step 4: Commit**

GitHub Desktop — message: `docs: document export/import and clarify the repository seam`

---

## Verification (M2, end-to-end)

- `dotnet build` — clean (0 warnings / 0 errors).
- `dotnet test` — all NUnit tests pass (M1 suite + ~8 new: 5 archive, 3 merge).
- `dotnet run --project Kenaz.Console` manual pass (Task 5, Step 4): export → file appears under `Documents\Kenaz` as a versioned envelope → import round-trips and preserves `CreatedAt`/`UpdatedAt` → newer-wins leaves a freshly-edited day unchanged → a bad path / corrupt file gives a calm message, never a crash → an export with a tampered out-of-range value is dropped and counted (`Skipped`).
- **MVC separation holds:** `Kenaz.Core` touches disk only in `Storage/`. Confirm:
  Run: `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'`
  Expected: no matches.

## Self-review (spec coverage)

- Repository interface formalized → Task 6 (seam doc; export/import kept off the contract).
- JSON export, versioned (`schemaVersion`) → Task 1.
- Validated import (reject/strip malformed; tolerate older versions) → Task 2.
- Plaintext-export warning → Task 4.
- Error handling (unreadable file → friendly; write failure → calm) → Tasks 2, 4, 5.
- Newer-wins merge, no data loss → Task 3.
- M6 XSS / `__proto__` guard → intentionally out of scope (no render layer yet).
