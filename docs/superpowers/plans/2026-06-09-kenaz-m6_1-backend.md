# Kenaz M6.1 Backend — InsightsService, GET /insights, Console Single-Sourcing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lift the insight gating out of the console into one Core service, expose it read-only over the loopback API, and rewire the console to consume it — so the console and (next plan) the web app share a single gating source.

**Architecture:** A new `InsightsService` in `Kenaz.Core/Services/` composes `WellbeingJournal`'s existing read methods into an `InsightsSummary` value type and owns the gating rules currently inline in the console. A new read-only `GET /insights` endpoint in `Kenaz.Api` (behind the existing bearer filter) returns it as a flattened DTO. The console's `ShowTodayVsWeek`/`ShowWeeklyReview` are refactored to render from the summary, deleting the duplicated rules.

**Tech Stack:** C# / .NET 10, NUnit 4.3.2, ASP.NET Core Minimal API, `WebApplicationFactory<Program>`, Microsoft.Data.Sqlite.

**Spec:** [docs/superpowers/specs/2026-06-09-kenaz-m6_1-pwa-design.md](../specs/2026-06-09-kenaz-m6_1-pwa-design.md) (this plan covers the backend half; the `Kenaz.Web` PWA is a separate plan).

---

## Conventions (apply to every task)

- **TDD:** write the failing test, run it red, implement the minimum to pass, run it green, then commit.
- **Commits:** one task = one commit, made by Malin via **GitHub Desktop** (the agent does not run `git commit`). **No `Co-Authored-By` trailer.** Each task ends with the exact suggested commit message.
- **Named constructor/method arguments** in tests (e.g. `Log(Today, mood: 8)`, `new CheckIn(day, mood: 9, …, createdAt: …, updatedAt: …)`) — the file-wide convention (M5's `7e08645` cleanup).
- **Test scaffolding already exists:** `Kenaz.Tests` references `Kenaz.Core` and `Kenaz.Api` and has `Microsoft.AspNetCore.Mvc.Testing` (added in M5). No `.csproj` changes are needed.
- **New Core files declare `namespace Kenaz.Core;`** even though they live in `Services/` (matches `WellbeingJournal.cs` / `SleepMoodPattern.cs`). New API files declare `namespace Kenaz.Api;`.
- **Run tests** from the repo root: `dotnet test Kenaz.slnx`. Filter with `--filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"` or `--filter "Name=<TestName>"`.

---

## File structure

```
Kenaz.Core/Services/
  InsightsSummary.cs      (NEW) value type + SleepTeaserDirection enum
  InsightsService.cs      (NEW) Summarize(now) → InsightsSummary; composes the journal; owns gating

Kenaz.Api/
  Contracts/InsightsResponse.cs   (NEW) InsightsResponse + DayHighlight + From(summary)
  Endpoints/InsightsEndpoints.cs  (NEW) GET /insights (read-only)
  Program.cs                      (MODIFY) register InsightsService; map the /insights group

Kenaz.Console/Program.cs          (MODIFY) ShowTodayVsWeek + ShowWeeklyReview render from InsightsService; Main wires it

Kenaz.Tests/
  InsightsServiceTests.cs   (NEW) ~13 gating-boundary unit tests
  InsightsResponseTests.cs  (NEW) 1 DTO mapping test
  InsightsApiTests.cs       (NEW) 4 WebApplicationFactory tests

README.md                   (MODIFY) add GET /insights to the Local API endpoint table
```

---

## Task 1: `InsightsSummary` + `InsightsService` skeleton (week glance)

Creates the value type and the service, implementing only the 7-day glance (averages, streak, `HasWeekData`). Highlights and the sleep–mood pattern are stubbed to defaults here and filled in by Tasks 2–3.

**Files:**
- Create: `Kenaz.Core/Services/InsightsSummary.cs`
- Create: `Kenaz.Core/Services/InsightsService.cs`
- Test: `Kenaz.Tests/InsightsServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Kenaz.Tests/InsightsServiceTests.cs`:

```csharp
using Kenaz.Core;

namespace Kenaz.Tests;

public class InsightsServiceTests
{
    // Anchored to local time so date derivation matches the journal on any machine (same as InsightTests).
    private static readonly DateTimeOffset Now = new DateTimeOffset(new DateTime(2026, 5, 22, 9, 0, 0, DateTimeKind.Local));
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.LocalDateTime);

    private WellbeingJournal _journal = null!;
    private InsightsService _insights = null!;

    [SetUp]
    public void SetUp()
    {
        var repository = new InMemoryCheckInRepository();
        _journal = new WellbeingJournal(repository, () => Now);
        _insights = new InsightsService(_journal);
    }

    private void Log(DateOnly date, int? mood = null, int? energy = null, decimal? sleep = null, string? note = null)
    {
        _journal.AddOrUpdate(date, mood, energy, sleep, note);
    }

    [Test]
    public void Summarize_on_empty_store_has_no_week_data_and_null_averages()
    {
        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasWeekData, Is.False);
        Assert.That(summary.MoodAverage, Is.Null);
        Assert.That(summary.EnergyAverage, Is.Null);
        Assert.That(summary.SleepAverage, Is.Null);
        Assert.That(summary.StreakDays, Is.EqualTo(0));
    }

    [Test]
    public void Summarize_computes_week_averages_over_the_7_day_window()
    {
        Log(Today, mood: 8, energy: 6, sleep: 7m);
        Log(Today.AddDays(-2), mood: 4, energy: 4, sleep: 5m);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.HasWeekData, Is.True);
        Assert.That(summary.MoodAverage, Is.EqualTo(6m));
        Assert.That(summary.EnergyAverage, Is.EqualTo(5m));
        Assert.That(summary.SleepAverage, Is.EqualTo(6m));
    }

    [Test]
    public void Summarize_passes_through_the_streak()
    {
        Log(Today, mood: 5);
        Log(Today.AddDays(-1), mood: 5);
        Log(Today.AddDays(-2), mood: 5);

        var summary = _insights.Summarize(Now);

        Assert.That(summary.StreakDays, Is.EqualTo(3));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"`
Expected: **build error** — `InsightsService` and `InsightsSummary` do not exist.

- [ ] **Step 3: Create the value type and enum**

Create `Kenaz.Core/Services/InsightsSummary.cs`:

```csharp
namespace Kenaz.Core;

/// <summary>
/// A read-only snapshot of the insights both clients render. Composed by <see cref="InsightsService"/>
/// from the journal's existing aggregates; it also carries the gating flags (what is showable) so a
/// View only chooses words, never re-derives a threshold.
/// </summary>
public sealed class InsightsSummary
{
    public InsightsSummary(
        decimal? moodAverage,
        decimal? energyAverage,
        decimal? sleepAverage,
        int streakDays,
        bool hasWeekData,
        CheckIn? brightestDay,
        CheckIn? hardestDay,
        bool hasHighlights,
        SleepMoodPattern sleepMood,
        bool showSleepTeaser,
        SleepTeaserDirection teaserDirection)
    {
        MoodAverage = moodAverage;
        EnergyAverage = energyAverage;
        SleepAverage = sleepAverage;
        StreakDays = streakDays;
        HasWeekData = hasWeekData;
        BrightestDay = brightestDay;
        HardestDay = hardestDay;
        HasHighlights = hasHighlights;
        SleepMood = sleepMood;
        ShowSleepTeaser = showSleepTeaser;
        TeaserDirection = teaserDirection;
    }

    // 7-day glance (null = not enough data for that metric)
    public decimal? MoodAverage { get; }
    public decimal? EnergyAverage { get; }
    public decimal? SleepAverage { get; }
    public int StreakDays { get; }
    public bool HasWeekData { get; }

    // 7-day mood highlights (null unless HasHighlights)
    public CheckIn? BrightestDay { get; }
    public CheckIn? HardestDay { get; }
    public bool HasHighlights { get; }

    // 30-day sleep–mood pattern (the existing M3 value type)
    public SleepMoodPattern SleepMood { get; }

    // Today-screen teaser gate (derived from the pattern)
    public bool ShowSleepTeaser { get; }
    public SleepTeaserDirection TeaserDirection { get; }
}

/// <summary>Which way the sleep–mood teaser leans, or None when it should not show.</summary>
public enum SleepTeaserDirection
{
    None,
    MoreSleepBetter,
    LessSleepBetter
}
```

- [ ] **Step 4: Create the service (glance only; highlights and pattern stubbed)**

Create `Kenaz.Core/Services/InsightsService.cs`:

```csharp
namespace Kenaz.Core;

/// <summary>
/// Composes the journal's existing read methods into one <see cref="InsightsSummary"/> and owns the
/// gating that decides what is showable. Read-only; takes <c>now</c> as a parameter (no injected clock)
/// so gating boundaries are deterministic in tests. Uses the same windows the console uses: 7 days for
/// the week glance and highlights, 30 days for the sleep–mood pattern.
/// </summary>
public sealed class InsightsService
{
    private const int WeekDays = 7;
    private const int PatternDays = 30;

    private readonly WellbeingJournal _journal;

    public InsightsService(WellbeingJournal journal)
    {
        _journal = journal;
    }

    public InsightsSummary Summarize(DateTimeOffset now)
    {
        var hasWeekData = _journal.Last7Days(now).Count > 0;

        // Highlights (Task 2) and the real pattern + teaser (Task 3) are stubbed here.
        var stubbedPattern = new SleepMoodPattern(
            threshold: WellbeingJournal.DefaultSleepThresholdHours,
            longSleepDays: 0,
            shortSleepDays: 0,
            longSleepMoodAverage: null,
            shortSleepMoodAverage: null,
            isConfident: false);

        return new InsightsSummary(
            moodAverage: _journal.Average(c => c.Mood, WeekDays, now),
            energyAverage: _journal.Average(c => c.Energy, WeekDays, now),
            sleepAverage: _journal.Average(c => c.Sleep, WeekDays, now),
            streakDays: _journal.StreakDays(now),
            hasWeekData: hasWeekData,
            brightestDay: null,
            hardestDay: null,
            hasHighlights: false,
            sleepMood: stubbedPattern,
            showSleepTeaser: false,
            teaserDirection: SleepTeaserDirection.None);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"`
Expected: **PASS** (3 tests).

- [ ] **Step 6: Commit (GitHub Desktop)**

Message: `feat(M6.1): InsightsService + InsightsSummary — week glance`

---

## Task 2: Highlights gating

Implement brightest/hardest with the "≥ 2 distinct moods" gate (`brightest.Date != hardest.Date`), over the 7-day window.

**Files:**
- Modify: `Kenaz.Core/Services/InsightsService.cs`
- Test: `Kenaz.Tests/InsightsServiceTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `InsightsServiceTests`:

```csharp
[Test]
public void Summarize_highlights_available_when_two_distinct_moods()
{
    Log(Today, mood: 9);
    Log(Today.AddDays(-1), mood: 3);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.HasHighlights, Is.True);
    Assert.That(summary.BrightestDay!.Date, Is.EqualTo(Today));
    Assert.That(summary.HardestDay!.Date, Is.EqualTo(Today.AddDays(-1)));
}

[Test]
public void Summarize_gates_highlights_when_all_moods_equal()
{
    Log(Today, mood: 6);
    Log(Today.AddDays(-1), mood: 6);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.HasHighlights, Is.False);
    Assert.That(summary.BrightestDay, Is.Null);
    Assert.That(summary.HardestDay, Is.Null);
}

[Test]
public void Summarize_gates_highlights_with_a_single_mood_day()
{
    Log(Today, mood: 7);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.HasHighlights, Is.False);
    Assert.That(summary.BrightestDay, Is.Null);
}

[Test]
public void Summarize_highlights_use_the_7_day_window()
{
    Log(Today.AddDays(-7), mood: 9);   // outside the window — must not become brightest
    Log(Today, mood: 5);
    Log(Today.AddDays(-1), mood: 3);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.HasHighlights, Is.True);
    Assert.That(summary.BrightestDay!.Date, Is.EqualTo(Today));
    Assert.That(summary.HardestDay!.Date, Is.EqualTo(Today.AddDays(-1)));
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"`
Expected: the 4 new tests **FAIL** (`HasHighlights` is stubbed `false`, days `null`).

- [ ] **Step 3: Implement highlights in `Summarize`**

In `InsightsService.Summarize`, replace the highlights stub. Add before the `return`:

```csharp
        var brightest = _journal.BestDay(c => c.Mood, WeekDays, now);
        var hardest = _journal.WorstDay(c => c.Mood, WeekDays, now);
        var hasHighlights = brightest is not null && hardest is not null && brightest.Date != hardest.Date;
```

and change the three highlight arguments in the `return`:

```csharp
            brightestDay: hasHighlights ? brightest : null,
            hardestDay: hasHighlights ? hardest : null,
            hasHighlights: hasHighlights,
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"`
Expected: **PASS** (7 tests).

- [ ] **Step 5: Commit (GitHub Desktop)**

Message: `feat(M6.1): InsightsService — brightest/hardest gating`

---

## Task 3: Sleep–mood pattern (teaser gate, direction, pattern pass-through)

Compute the real 30-day pattern and derive the Today-screen teaser: shown only when `IsConfident` **and** `|LongAvg − ShortAvg| ≥ 1.0`, with direction from the sign of the gap.

**Files:**
- Modify: `Kenaz.Core/Services/InsightsService.cs`
- Test: `Kenaz.Tests/InsightsServiceTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `InsightsServiceTests`:

```csharp
[Test]
public void Summarize_shows_teaser_when_confident_and_gap_at_least_one()
{
    // 5 long-sleep days mood 8 (avg 8), 5 short-sleep days mood 7 (avg 7) → gap exactly 1.0
    for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
    for (var i = 5; i < 10; i++) Log(Today.AddDays(-i), mood: 7, sleep: 6m);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.SleepMood.IsConfident, Is.True);
    Assert.That(summary.ShowSleepTeaser, Is.True);
    Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.MoreSleepBetter));
}

[Test]
public void Summarize_hides_teaser_when_gap_below_one()
{
    // long avg 8, short avg 7.2 ({8,7,7,7,7}) → gap 0.8 < 1.0 → hidden
    for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
    Log(Today.AddDays(-5), mood: 8, sleep: 6m);
    for (var i = 6; i < 10; i++) Log(Today.AddDays(-i), mood: 7, sleep: 6m);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.SleepMood.IsConfident, Is.True);
    Assert.That(summary.ShowSleepTeaser, Is.False);
    Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.None));
}

[Test]
public void Summarize_hides_teaser_when_not_confident()
{
    // 4 long + 5 short (long bucket one below the floor) — large gap, but not confident
    for (var i = 0; i < 4; i++) Log(Today.AddDays(-i), mood: 9, sleep: 8m);
    for (var i = 4; i < 9; i++) Log(Today.AddDays(-i), mood: 3, sleep: 6m);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.SleepMood.IsConfident, Is.False);
    Assert.That(summary.ShowSleepTeaser, Is.False);
}

[Test]
public void Summarize_teaser_direction_is_less_sleep_better_at_negative_gap()
{
    // long avg 7, short avg 8 → gap -1.0 → shown, shorter nights felt better
    for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 7, sleep: 8m);
    for (var i = 5; i < 10; i++) Log(Today.AddDays(-i), mood: 8, sleep: 6m);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.ShowSleepTeaser, Is.True);
    Assert.That(summary.TeaserDirection, Is.EqualTo(SleepTeaserDirection.LessSleepBetter));
}

[Test]
public void Summarize_pattern_confident_carries_threshold_counts_and_averages()
{
    for (var i = 0; i < 5; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
    for (var i = 5; i < 10; i++) Log(Today.AddDays(-i), mood: 5, sleep: 6m);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.SleepMood.IsConfident, Is.True);
    Assert.That(summary.SleepMood.Threshold, Is.EqualTo(7m));
    Assert.That(summary.SleepMood.LongSleepDays, Is.EqualTo(5));
    Assert.That(summary.SleepMood.ShortSleepDays, Is.EqualTo(5));
    Assert.That(summary.SleepMood.LongSleepMoodAverage, Is.EqualTo(8m));
    Assert.That(summary.SleepMood.ShortSleepMoodAverage, Is.EqualTo(5m));
}

[Test]
public void Summarize_pattern_not_confident_below_min_days_per_bucket()
{
    // 4 long + 4 short — both below the floor of 5
    for (var i = 0; i < 4; i++) Log(Today.AddDays(-i), mood: 8, sleep: 8m);
    for (var i = 4; i < 8; i++) Log(Today.AddDays(-i), mood: 5, sleep: 6m);

    var summary = _insights.Summarize(Now);

    Assert.That(summary.SleepMood.IsConfident, Is.False);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"`
Expected: the 6 new tests **FAIL** (pattern is the stub; `ShowSleepTeaser` is `false`).

- [ ] **Step 3: Compute the real pattern and the teaser**

In `InsightsService`, delete the `stubbedPattern` block and compute the real pattern; add the teaser helper. The `Summarize` body becomes:

```csharp
    public InsightsSummary Summarize(DateTimeOffset now)
    {
        var hasWeekData = _journal.Last7Days(now).Count > 0;

        var brightest = _journal.BestDay(c => c.Mood, WeekDays, now);
        var hardest = _journal.WorstDay(c => c.Mood, WeekDays, now);
        var hasHighlights = brightest is not null && hardest is not null && brightest.Date != hardest.Date;

        var pattern = _journal.SleepMoodPattern(PatternDays, WellbeingJournal.DefaultSleepThresholdHours, now);
        var (showTeaser, direction) = TeaserFrom(pattern);

        return new InsightsSummary(
            moodAverage: _journal.Average(c => c.Mood, WeekDays, now),
            energyAverage: _journal.Average(c => c.Energy, WeekDays, now),
            sleepAverage: _journal.Average(c => c.Sleep, WeekDays, now),
            streakDays: _journal.StreakDays(now),
            hasWeekData: hasWeekData,
            brightestDay: hasHighlights ? brightest : null,
            hardestDay: hasHighlights ? hardest : null,
            hasHighlights: hasHighlights,
            sleepMood: pattern,
            showSleepTeaser: showTeaser,
            teaserDirection: direction);
    }

    private static (bool show, SleepTeaserDirection direction) TeaserFrom(SleepMoodPattern p)
    {
        if (!p.IsConfident
            || p.LongSleepMoodAverage is not { } longAvg
            || p.ShortSleepMoodAverage is not { } shortAvg)
        {
            return (false, SleepTeaserDirection.None);
        }

        var gap = longAvg - shortAvg;
        if (gap >= 1.0m) return (true, SleepTeaserDirection.MoreSleepBetter);
        if (gap <= -1.0m) return (true, SleepTeaserDirection.LessSleepBetter);
        return (false, SleepTeaserDirection.None);
    }
```

- [ ] **Step 4: Run the full `InsightsServiceTests` to verify all pass**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsServiceTests"`
Expected: **PASS** (13 tests).

- [ ] **Step 5: Commit (GitHub Desktop)**

Message: `feat(M6.1): InsightsService — sleep–mood teaser gate + pattern`

---

## Task 4: `InsightsResponse` DTO

The wire shape: flatten `InsightsSummary` + its embedded `SleepMoodPattern`, dates as `yyyy-MM-dd`, the enum as a string, highlights slimmed to a `DayHighlight` (no note). Mirrors `CheckInResponse`.

**Files:**
- Create: `Kenaz.Api/Contracts/InsightsResponse.cs`
- Test: `Kenaz.Tests/InsightsResponseTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Kenaz.Tests/InsightsResponseTests.cs`:

```csharp
using Kenaz.Api;
using Kenaz.Core;

namespace Kenaz.Tests;

public class InsightsResponseTests
{
    [Test]
    public void From_flattens_summary_and_pattern_with_iso_dates_and_string_direction()
    {
        var brightest = new CheckIn(new DateOnly(2026, 5, 27), mood: 9, energy: 8, sleep: 7.5m, note: "bright",
            createdAt: DateTimeOffset.UnixEpoch, updatedAt: DateTimeOffset.UnixEpoch);
        var hardest = new CheckIn(new DateOnly(2026, 5, 26), mood: 3, energy: 4, sleep: 5m, note: "hard",
            createdAt: DateTimeOffset.UnixEpoch, updatedAt: DateTimeOffset.UnixEpoch);
        var pattern = new SleepMoodPattern(threshold: 7m, longSleepDays: 6, shortSleepDays: 5,
            longSleepMoodAverage: 8m, shortSleepMoodAverage: 6m, isConfident: true);
        var summary = new InsightsSummary(
            moodAverage: 7.5m, energyAverage: 6m, sleepAverage: 7m, streakDays: 4, hasWeekData: true,
            brightestDay: brightest, hardestDay: hardest, hasHighlights: true,
            sleepMood: pattern, showSleepTeaser: true, teaserDirection: SleepTeaserDirection.MoreSleepBetter);

        var dto = InsightsResponse.From(summary);

        Assert.That(dto.MoodAverage, Is.EqualTo(7.5m));
        Assert.That(dto.StreakDays, Is.EqualTo(4));
        Assert.That(dto.HasWeekData, Is.True);
        Assert.That(dto.BrightestDay!.Date, Is.EqualTo("2026-05-27"));
        Assert.That(dto.BrightestDay.Mood, Is.EqualTo(9));
        Assert.That(dto.HardestDay!.Date, Is.EqualTo("2026-05-26"));
        Assert.That(dto.SleepThreshold, Is.EqualTo(7m));
        Assert.That(dto.LongSleepDays, Is.EqualTo(6));
        Assert.That(dto.LongSleepMoodAverage, Is.EqualTo(8m));
        Assert.That(dto.SleepPatternConfident, Is.True);
        Assert.That(dto.ShowSleepTeaser, Is.True);
        Assert.That(dto.TeaserDirection, Is.EqualTo("MoreSleepBetter"));
    }

    [Test]
    public void From_maps_absent_highlights_to_null()
    {
        var pattern = new SleepMoodPattern(threshold: 7m, longSleepDays: 0, shortSleepDays: 0,
            longSleepMoodAverage: null, shortSleepMoodAverage: null, isConfident: false);
        var summary = new InsightsSummary(
            moodAverage: null, energyAverage: null, sleepAverage: null, streakDays: 0, hasWeekData: false,
            brightestDay: null, hardestDay: null, hasHighlights: false,
            sleepMood: pattern, showSleepTeaser: false, teaserDirection: SleepTeaserDirection.None);

        var dto = InsightsResponse.From(summary);

        Assert.That(dto.BrightestDay, Is.Null);
        Assert.That(dto.HardestDay, Is.Null);
        Assert.That(dto.TeaserDirection, Is.EqualTo("None"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsResponseTests"`
Expected: **build error** — `InsightsResponse` does not exist.

- [ ] **Step 3: Create the DTO**

Create `Kenaz.Api/Contracts/InsightsResponse.cs`:

```csharp
using System.Globalization;
using Kenaz.Core;

namespace Kenaz.Api;

/// <summary>A brightest/hardest day on the wire — date as yyyy-MM-dd, no note.</summary>
public record DayHighlight(string Date, int? Mood, int? Energy, decimal? Sleep);

/// <summary>The wire shape of <see cref="InsightsSummary"/>: flattened, dates as yyyy-MM-dd, enum as string.</summary>
public record InsightsResponse(
    decimal? MoodAverage,
    decimal? EnergyAverage,
    decimal? SleepAverage,
    int StreakDays,
    bool HasWeekData,
    DayHighlight? BrightestDay,
    DayHighlight? HardestDay,
    bool HasHighlights,
    decimal SleepThreshold,
    int LongSleepDays,
    int ShortSleepDays,
    decimal? LongSleepMoodAverage,
    decimal? ShortSleepMoodAverage,
    bool SleepPatternConfident,
    bool ShowSleepTeaser,
    string TeaserDirection)
{
    public static InsightsResponse From(InsightsSummary s) => new InsightsResponse(
        MoodAverage: s.MoodAverage,
        EnergyAverage: s.EnergyAverage,
        SleepAverage: s.SleepAverage,
        StreakDays: s.StreakDays,
        HasWeekData: s.HasWeekData,
        BrightestDay: ToHighlight(s.BrightestDay),
        HardestDay: ToHighlight(s.HardestDay),
        HasHighlights: s.HasHighlights,
        SleepThreshold: s.SleepMood.Threshold,
        LongSleepDays: s.SleepMood.LongSleepDays,
        ShortSleepDays: s.SleepMood.ShortSleepDays,
        LongSleepMoodAverage: s.SleepMood.LongSleepMoodAverage,
        ShortSleepMoodAverage: s.SleepMood.ShortSleepMoodAverage,
        SleepPatternConfident: s.SleepMood.IsConfident,
        ShowSleepTeaser: s.ShowSleepTeaser,
        TeaserDirection: s.TeaserDirection.ToString());

    private static DayHighlight? ToHighlight(CheckIn? c) => c is null
        ? null
        : new DayHighlight(c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), c.Mood, c.Energy, c.Sleep);
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsResponseTests"`
Expected: **PASS** (2 tests).

- [ ] **Step 5: Commit (GitHub Desktop)**

Message: `feat(M6.1): InsightsResponse DTO`

---

## Task 5: `GET /insights` endpoint + DI

Register `InsightsService` and map a read-only `/insights` group behind the existing bearer filter — mirroring the `/checkins` wiring.

**Files:**
- Create: `Kenaz.Api/Endpoints/InsightsEndpoints.cs`
- Modify: `Kenaz.Api/Program.cs`
- Test: `Kenaz.Tests/InsightsApiTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `Kenaz.Tests/InsightsApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kenaz.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Kenaz.Tests;

public class InsightsApiTests
{
    private const string Token = "test-token-abc123";

    private string _dir = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "kenaz-insights-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var dbPath = Path.Combine(_dir, "checkins.db");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Kenaz:DbPath", dbPath);
            b.UseSetting("Kenaz:Token", Token);
        });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Test]
    public async Task Get_insights_without_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/insights");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.WwwAuthenticate.Single().Scheme, Is.EqualTo("Bearer"));
    }

    [Test]
    public async Task Get_insights_with_wrong_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-token");

        var response = await _client.GetAsync("/insights");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_insights_on_empty_store_returns_200_with_no_data_flags()
    {
        var insights = await _client.GetFromJsonAsync<InsightsResponse>("/insights");

        Assert.That(insights, Is.Not.Null);
        Assert.That(insights!.HasWeekData, Is.False);
        Assert.That(insights.MoodAverage, Is.Null);
        Assert.That(insights.HasHighlights, Is.False);
        Assert.That(insights.SleepPatternConfident, Is.False);
        Assert.That(insights.ShowSleepTeaser, Is.False);
    }

    [Test]
    public async Task Get_insights_with_seeded_data_returns_computed_values_and_flags()
    {
        // The endpoint summarizes over DateTimeOffset.Now, so seed dates relative to the real today.
        var today = DateOnly.FromDateTime(DateTime.Now);

        // 5 long-sleep days mood 8 + 5 short-sleep days mood 5: confident, gap 3 → teaser, more-sleep-better.
        for (var i = 0; i < 5; i++)
        {
            await _client.PutAsJsonAsync($"/checkins/{today.AddDays(-i):yyyy-MM-dd}",
                new UpsertCheckInRequest(Mood: 8, Energy: null, Sleep: 8m, Note: null));
        }
        for (var i = 5; i < 10; i++)
        {
            await _client.PutAsJsonAsync($"/checkins/{today.AddDays(-i):yyyy-MM-dd}",
                new UpsertCheckInRequest(Mood: 5, Energy: null, Sleep: 6m, Note: null));
        }

        var insights = await _client.GetFromJsonAsync<InsightsResponse>("/insights");

        Assert.That(insights!.HasWeekData, Is.True);
        Assert.That(insights.MoodAverage, Is.Not.Null);
        Assert.That(insights.HasHighlights, Is.True);
        Assert.That(insights.BrightestDay!.Date, Is.EqualTo($"{today:yyyy-MM-dd}"));
        Assert.That(insights.SleepPatternConfident, Is.True);
        Assert.That(insights.ShowSleepTeaser, Is.True);
        Assert.That(insights.TeaserDirection, Is.EqualTo("MoreSleepBetter"));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsApiTests"`
Expected: **build error** (`MapInsightsEndpoints` doesn't exist) — and once that's added but unmapped, the 200 tests would 404. Either way: red.

- [ ] **Step 3: Create the endpoint**

Create `Kenaz.Api/Endpoints/InsightsEndpoints.cs`:

```csharp
using Kenaz.Core;

namespace Kenaz.Api;

public static class InsightsEndpoints
{
    public static RouteGroupBuilder MapInsightsEndpoints(this RouteGroupBuilder group)
    {
        // Read-only: no WriteLock. Summarizes over the current wall clock.
        group.MapGet("/", (InsightsService insights) =>
            Results.Ok(InsightsResponse.From(insights.Summarize(DateTimeOffset.Now))));

        return group;
    }
}
```

- [ ] **Step 4: Register the service and map the group in `Program.cs`**

In `Kenaz.Api/Program.cs`, after the `WriteLock` registration (the line `builder.Services.AddSingleton<WriteLock>();`), add:

```csharp
builder.Services.AddSingleton(sp => new InsightsService(sp.GetRequiredService<WellbeingJournal>()));
```

And after the existing `app.MapGroup("/checkins")…MapCheckInEndpoints();` block, add:

```csharp
app.MapGroup("/insights")
   .AddEndpointFilter<BearerTokenFilter>()
   .MapInsightsEndpoints();
```

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test Kenaz.slnx --filter "FullyQualifiedName~Kenaz.Tests.InsightsApiTests"`
Expected: **PASS** (4 tests).

- [ ] **Step 6: Commit (GitHub Desktop)**

Message: `feat(M6.1): GET /insights endpoint (read-only, token-guarded)`

---

## Task 6: Console renders from `InsightsService` (single-source the gating)

Refactor `ShowTodayVsWeek` and `ShowWeeklyReview` to read an `InsightsSummary` instead of computing gating inline. **The wording is unchanged** — only the source of the flags/values moves. Verified by running (the console has no automated render tests, by convention).

**Files:**
- Modify: `Kenaz.Console/Program.cs`

- [ ] **Step 1: Capture the pre-refactor baseline**

Run the console and record the exact "Today vs your last 7 days" and "Weekly review" output for the current data:

Run: `'2`n6`n0' | dotnet run --project Kenaz.Console`
Save the output (it will show the "Not enough check-ins yet…" branch for both, since the only real check-in is older than 7 days). This is the parity baseline.

- [ ] **Step 2: Wire `InsightsService` into `Main`**

In `Program.cs`, immediately after the journal is created (`var journal = new WellbeingJournal(repository, () => DateTimeOffset.Now);`), add:

```csharp
        var insights = new InsightsService(journal);
```

Change the two menu cases to pass it:

```csharp
                case "2":
                    ShowTodayVsWeek(journal, insights);
                    break;
```
```csharp
                case "6":
                    ShowWeeklyReview(insights);
                    break;
```

- [ ] **Step 3: Replace `ShowTodayVsWeek`**

Replace the entire `ShowTodayVsWeek` method with:

```csharp
    private static void ShowTodayVsWeek(WellbeingJournal journal, InsightsService insights)
    {
        var now = DateTimeOffset.Now;
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var todayCheckIn = journal.GetByDate(today);
        var summary = insights.Summarize(now);

        WriteLine();
        if (!summary.HasWeekData)
        {
            WriteLine("Not enough check-ins yet — check in a few days and your patterns will show up here.");
            return;
        }

        WriteLine(todayCheckIn is null
            ? "You haven't checked in today yet. Here's your last 7 days:"
            : "Today vs your last 7 days:");

        WriteLine($"  Mood     today {Scale(todayCheckIn?.Mood)}    7-day avg {Average(summary.MoodAverage)}");
        WriteLine($"  Energy   today {Scale(todayCheckIn?.Energy)}    7-day avg {Average(summary.EnergyAverage)}");
        WriteLine($"  Sleep    today {Hours(todayCheckIn?.Sleep)}    7-day avg {AverageHours(summary.SleepAverage)}");
        WriteLine();
        WriteLine(StreakMessage(summary.StreakDays));

        if (summary.ShowSleepTeaser)
        {
            WriteLine();
            WriteLine(summary.TeaserDirection == SleepTeaserDirection.MoreSleepBetter
                ? "A small pattern: you've felt better on nights with more sleep. Open Weekly review for more."
                : "A small pattern: you've felt better on shorter-sleep nights. Open Weekly review for more.");
        }
    }
```

- [ ] **Step 4: Replace `ShowWeeklyReview`**

Replace the entire `ShowWeeklyReview` method with:

```csharp
    private static void ShowWeeklyReview(InsightsService insights)
    {
        var now = DateTimeOffset.Now;
        var summary = insights.Summarize(now);

        WriteLine();
        if (!summary.HasWeekData)
        {
            WriteLine("Not enough check-ins yet — your weekly review will appear here once you've logged a few days.");
            return;
        }

        WriteLine("Your week");
        WriteLine($"  Mood     avg {Average(summary.MoodAverage)}   (last 7 days)");
        WriteLine($"  Energy   avg {Average(summary.EnergyAverage)}");
        WriteLine($"  Sleep    avg {AverageHours(summary.SleepAverage)}");
        WriteLine($"  {StreakMessage(summary.StreakDays)}");

        if (summary.HasHighlights)
        {
            WriteLine();
            WriteLine($"  Brightest day  {FormatDay(summary.BrightestDay!)}    mood {Scale(summary.BrightestDay!.Mood)}   energy {Scale(summary.BrightestDay!.Energy)}   sleep {Hours(summary.BrightestDay!.Sleep)}");
            WriteLine($"  Hardest day    {FormatDay(summary.HardestDay!)}     mood {Scale(summary.HardestDay!.Mood)}   energy {Scale(summary.HardestDay!.Energy)}   sleep {Hours(summary.HardestDay!.Sleep)}");
        }

        WriteLine();
        WriteLine("A small pattern");
        var pattern = summary.SleepMood;
        if (pattern.IsConfident)
        {
            var longAvg = pattern.LongSleepMoodAverage!.Value;
            var shortAvg = pattern.ShortSleepMoodAverage!.Value;
            WriteLine($"  On nights you slept ≥{pattern.Threshold:0.#} h, your mood averaged {longAvg:0.0} — vs {shortAvg:0.0} on shorter nights.");
            WriteLine($"  (last 30 days: {pattern.LongSleepDays} longer-sleep days, {pattern.ShortSleepDays} shorter)");
        }
        else
        {
            WriteLine("  Not enough sleep + mood data yet — keep logging both and the pattern will show up here.");
        }
    }
```

- [ ] **Step 5: Build, then run and confirm parity**

Run: `dotnet build Kenaz.slnx`
Expected: 0 warnings / 0 errors.

Run: `'2`n6`n0' | dotnet run --project Kenaz.Console`
Expected: byte-identical to the Step 1 baseline.

> **Note on richer branches:** the real store has one check-in older than 7 days, so the run exercises the `HasWeekData == false` branch. Value-parity for the populated branches (averages, brightest/hardest, the pattern card) is guaranteed two ways: `InsightsService` *composes the same journal methods* the old code called inline (proven identical by Tasks 1–3), and every copy string above is copied verbatim from the pre-refactor method. The populated branches are also exercised live by the seeded `InsightsApiTests` (Task 5), which read through the same `InsightsService`.

- [ ] **Step 6: Confirm the whole suite is still green**

Run: `dotnet test Kenaz.slnx`
Expected: **PASS** (~135 total).

- [ ] **Step 7: Commit (GitHub Desktop)**

Message: `refactor(M6.1): console renders insights from InsightsService (single source)`

---

## Task 7: README — add `GET /insights` to the Local API table

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add the endpoint row**

In the `## Local API` section's endpoint table, add a row for the new endpoint (match the existing table's column layout):

```markdown
| `GET`    | `/insights`        | Computed insights (7-day averages, streak, brightest/hardest, sleep–mood pattern) + gating flags. Read-only. |
```

- [ ] **Step 2: Verify the table renders**

Open `README.md` and confirm the new row aligns with the existing `/checkins` rows and the surrounding prose still reads correctly (still loopback-only, still token-guarded).

- [ ] **Step 3: Commit (GitHub Desktop)**

Message: `docs(M6.1): README — GET /insights endpoint`

---

## Task 8: Final backend verification

No code — confirm the backend slice is green and clean end-to-end.

- [ ] **Step 1: Clean build**

Run: `dotnet build Kenaz.slnx`
Expected: **0 warnings / 0 errors**.

- [ ] **Step 2: Full test suite**

Run: `dotnet test Kenaz.slnx`
Expected: **PASS, ~136 tests** (117 prior + 13 `InsightsServiceTests` + 2 `InsightsResponseTests` + 4 `InsightsApiTests` = 136; confirm the exact number printed).

- [ ] **Step 3: MVC separation check**

Run: `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'`
Expected: **no matches** — `InsightsService` is pure composition, no IO.

- [ ] **Step 4: Console parity (one more look)**

Run: `'2`n6`n0' | dotnet run --project Kenaz.Console`
Expected: matches the Task 6 baseline; the app launches, the menu works, and both insight screens render their current-data branch unchanged.

- [ ] **Step 5: No commit**

Verification only.

---

## Self-Review (plan vs spec, backend scope)

**Spec coverage:**
- *`InsightsService` composes the journal, owns gating; `now` is a method param; no `WellbeingJournal` change* → Tasks 1–3 (constructor takes only the journal; `Summarize(DateTimeOffset now)`; journal untouched). ✓
- *Gating rules match the console exactly* (teaser `IsConfident && |gap| ≥ 1.0` + direction; highlights `brightest.Date != hardest.Date`; whole-section `HasWeekData`) → Tasks 1–3 tests at the boundaries. ✓
- *`GET /insights` behind the existing bearer filter, read-only, no query params, flattened DTO* → Tasks 4–5. ✓
- *Console refactored to consume it; copy unchanged; parity by running* → Task 6. ✓
- *~18 new tests, ~135 total* → 13 + 2 + 4 = 19 new (≈ spec's ~18; the extra is the second DTO null-mapping test), 136 total. ✓
- *README note* → Task 7 (the endpoint table; the "Desktop app" run note belongs to the web plan). ✓

**Placeholder scan:** none — every step has the actual test code, implementation, command, and expected result.

**Type consistency:** `InsightsSummary` constructor argument order/names are identical across Tasks 1, 4 (test), and the DTO mapper; `SleepTeaserDirection` values (`None`/`MoreSleepBetter`/`LessSleepBetter`) are consistent in the enum, `TeaserFrom`, the console, and the `.ToString()` wire mapping; `InsightsService(journal)` + `Summarize(now)` signatures match across Core, DI registration, and the console.

**Out of scope (this plan):** the `Kenaz.Web` project, same-origin serving, `wwwroot`, auth/Setup, the four screens, the error banner, and the "Desktop app" README note — all in the separate web plan.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-09-kenaz-m6_1-backend.md`. Two execution options:

1. **Subagent-Driven (recommended)** — a fresh subagent per task, two-stage review between tasks, fast iteration. Matches how M5 ran.
2. **Inline Execution** — execute tasks in this session with checkpoints for review.

Which approach? (And the `Kenaz.Web` frontend plan is the next thing to write — before or after executing this backend plan, your call.)
