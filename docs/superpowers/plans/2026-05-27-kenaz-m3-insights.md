# Kenaz M3 — Insights: Weekly Review + First Correlation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the user a dedicated weekly review (averages + brightest/hardest day + a first sleep–mood correlation) and a quiet teaser line on the after-check-in glance, all driven by pure functions in Core.

**Architecture:** Three new pure helpers on `WellbeingJournal` (`BestDay`, `WorstDay`, `SleepMoodPattern`) plus one small value type (`SleepMoodPattern`). Two console additions (new option 6 "Weekly review" + a teaser at the end of option 2). One Program-startup line to set the console to UTF-8 so `≥` and Norwegian weekdays render correctly across shells. Zero new IO in Core; Core never references `Console`.

**Tech Stack:** C# / net10.0, LINQ, NUnit 4.x. No new NuGet packages.

---

## Context

M1 shipped the daily tool (check-in, history, today-vs-week glance, gentle streak) and the insight primitives (`Last7Days`, `Average`, `StreakDays`). M2 shipped data portability (versioned export/import, newer-wins merge). M3 is the **insight tier** from the original spec ([2026-05-21-kenaz-design.md](../specs/2026-05-21-kenaz-design.md), milestone table line 82): *"Insights: weekly review, averages, gentle streak, first correlation (pure functions)."*

Since averages and streak already shipped and are rendered in option 2, M3's net-new work is narrower than the milestone line suggests: a **weekly-review view** distinct from the after-check-in glance (with brightest/hardest day callouts), and the **first correlation** insight the original spec hints at ("better on days you slept >7h").

Design spec (read this first): [2026-05-27-kenaz-m3-insights.md](../specs/2026-05-27-kenaz-m3-insights.md). All decisions, gating rules, and copy choices are pinned there.

**Learning mode applies** (per original spec): each task carries a short `> Concept:` note on the new C# idea before the code.

---

## File Structure

**Create:**
- `Kenaz.Core/Services/SleepMoodPattern.cs` — public value type carrying counts, averages, and `IsConfident`.

**Modify:**
- `Kenaz.Core/Services/WellbeingJournal.cs` — add `BestDay`, `WorstDay`, `SleepMoodPattern` methods + two public constants.
- `Kenaz.Tests/InsightTests.cs` — add tests for the three new helpers (extends the existing fixture).
- `Kenaz.Console/Program.cs` — set `Console.OutputEncoding`, add menu option 6 + `ShowWeeklyReview`, append teaser to `ShowTodayVsWeek`, add a `FormatDay` helper.
- `README.md` — one-sentence update to the "what the app does" line.

**Reuse (do not duplicate the ideas, follow the patterns):**
- The `Func<CheckIn, decimal?>` + window + injected clock pattern from `WellbeingJournal.Average` ([Kenaz.Core/Services/WellbeingJournal.cs:100-111](../../Kenaz.Core/Services/WellbeingJournal.cs)).
- The fixed-`Now`, `InMemoryCheckInRepository`, `Log(...)` helper fixture from [Kenaz.Tests/InsightTests.cs](../../Kenaz.Tests/InsightTests.cs).
- View helpers (`Scale`, `Hours`, `Average`, `AverageHours`, `StreakMessage`) from [Kenaz.Console/Program.cs](../../Kenaz.Console/Program.cs) — reuse for consistent formatting.

> **Commits:** one task = one commit, made in **GitHub Desktop** (no `Co-Authored-By`). Push is Desktop-only and your call. Commit messages below are suggestions.

---

### Task 1: BestDay / WorstDay helpers

**Files:**
- Modify: `Kenaz.Core/Services/WellbeingJournal.cs`
- Test: `Kenaz.Tests/InsightTests.cs`

> Concept: a `Func<CheckIn, decimal?>` selector lets one method answer "best day by Mood" or "best day by Energy" without duplicating the windowing/null-handling. LINQ's `OrderByDescending(...).ThenByDescending(c => c.Date).FirstOrDefault()` gives a clean tie-breaker — the secondary sort only kicks in when the primary key ties. Same selector shape as the existing `Average`, so reading the journal stays uniform.

- [ ] **Step 1: Write the failing tests**

Append to `Kenaz.Tests/InsightTests.cs` (inside the existing class, after `StreakDays_breaks_on_two_consecutive_gaps`):

```csharp
[Test]
public void BestDay_returns_null_when_window_is_empty()
{
    var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

    Assert.That(best, Is.Null);
}

[Test]
public void BestDay_returns_null_when_no_day_in_window_has_the_selected_field()
{
    Log(Today, note: "note only");
    Log(Today.AddDays(-1), note: "note only");

    var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

    Assert.That(best, Is.Null);
}

[Test]
public void BestDay_picks_the_day_with_the_max_value()
{
    Log(Today, mood: 5);
    Log(Today.AddDays(-1), mood: 9);
    Log(Today.AddDays(-2), mood: 3);

    var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

    Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-1)));
}

[Test]
public void BestDay_ignores_days_where_the_selector_is_null()
{
    Log(Today, mood: 5);
    Log(Today.AddDays(-1), note: "no mood today-1");
    Log(Today.AddDays(-2), mood: 7);

    var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

    Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-2)));
}

[Test]
public void BestDay_tie_breaker_picks_the_most_recent_date()
{
    Log(Today.AddDays(-3), mood: 7);
    Log(Today.AddDays(-1), mood: 7);
    Log(Today.AddDays(-2), mood: 7);

    var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

    Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-1)));
}

[Test]
public void BestDay_excludes_the_seventh_day_back()
{
    Log(Today.AddDays(-7), mood: 9);
    Log(Today.AddDays(-1), mood: 3);

    var best = _journal.BestDay(c => c.Mood, days: 7, now: Now);

    Assert.That(best!.Date, Is.EqualTo(Today.AddDays(-1)));
}

[Test]
public void WorstDay_picks_the_day_with_the_min_value()
{
    Log(Today, mood: 5);
    Log(Today.AddDays(-1), mood: 3);
    Log(Today.AddDays(-2), mood: 9);

    var worst = _journal.WorstDay(c => c.Mood, days: 7, now: Now);

    Assert.That(worst!.Date, Is.EqualTo(Today.AddDays(-1)));
}

[Test]
public void WorstDay_tie_breaker_picks_the_most_recent_date()
{
    Log(Today.AddDays(-3), mood: 2);
    Log(Today.AddDays(-1), mood: 2);
    Log(Today.AddDays(-2), mood: 2);

    var worst = _journal.WorstDay(c => c.Mood, days: 7, now: Now);

    Assert.That(worst!.Date, Is.EqualTo(Today.AddDays(-1)));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~InsightTests.BestDay|FullyQualifiedName~InsightTests.WorstDay"`
Expected: FAIL — `BestDay` / `WorstDay` do not exist (compile error).

- [ ] **Step 3: Add BestDay and WorstDay to `WellbeingJournal`**

In `Kenaz.Core/Services/WellbeingJournal.cs`, after `Average` and before `StreakDays`:

```csharp
/// <summary>
/// The day in the window with the highest value of <paramref name="selector"/>, or null if no day
/// in the window has a value. Tie-breaker: most recent date wins.
/// </summary>
public CheckIn? BestDay(Func<CheckIn, decimal?> selector, int days, DateTimeOffset now)
{
    var today = Today(now);
    var start = today.AddDays(-(days - 1));

    return _repository.LoadAll()
        .Where(c => c.Date >= start && c.Date <= today)
        .Where(c => selector(c).HasValue)
        .OrderByDescending(c => selector(c)!.Value)
        .ThenByDescending(c => c.Date)
        .FirstOrDefault();
}

/// <summary>
/// The day in the window with the lowest value of <paramref name="selector"/>, or null if no day
/// in the window has a value. Tie-breaker: most recent date wins (same as <see cref="BestDay"/>).
/// </summary>
public CheckIn? WorstDay(Func<CheckIn, decimal?> selector, int days, DateTimeOffset now)
{
    var today = Today(now);
    var start = today.AddDays(-(days - 1));

    return _repository.LoadAll()
        .Where(c => c.Date >= start && c.Date <= today)
        .Where(c => selector(c).HasValue)
        .OrderBy(c => selector(c)!.Value)
        .ThenByDescending(c => c.Date)
        .FirstOrDefault();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~InsightTests.BestDay|FullyQualifiedName~InsightTests.WorstDay"`
Expected: PASS (8 tests).

- [ ] **Step 5: Run the full suite to confirm nothing else regressed**

Run: `dotnet test`
Expected: PASS — all M1 + M2 + new tests green (60 total).

- [ ] **Step 6: Commit**

GitHub Desktop — message: `feat: add BestDay and WorstDay insight helpers`

---

### Task 2: SleepMoodPattern value type + journal method + constants

**Files:**
- Create: `Kenaz.Core/Services/SleepMoodPattern.cs`
- Modify: `Kenaz.Core/Services/WellbeingJournal.cs`
- Test: `Kenaz.Tests/InsightTests.cs`

> Concept: a small value type (`sealed class` with `get`-only properties) carries the bucket-compare result so the view never has to reimplement the confidence rule. We pre-filter to *qualified days* (both mood and sleep present) — that's why a non-empty bucket always has a non-null average, and why the confidence rule reduces to just two count checks. Constants on `WellbeingJournal` give one discoverable place to tune the threshold and the gate.

- [ ] **Step 1: Write the failing tests**

Append to `Kenaz.Tests/InsightTests.cs` (after the BestDay/WorstDay tests from Task 1):

```csharp
[Test]
public void SleepMoodPattern_returns_zeros_and_nulls_on_empty_window()
{
    var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

    Assert.That(pattern.LongSleepDays, Is.EqualTo(0));
    Assert.That(pattern.ShortSleepDays, Is.EqualTo(0));
    Assert.That(pattern.LongSleepMoodAverage, Is.Null);
    Assert.That(pattern.ShortSleepMoodAverage, Is.Null);
    Assert.That(pattern.IsConfident, Is.False);
}

[Test]
public void SleepMoodPattern_excludes_days_missing_either_sleep_or_mood()
{
    Log(Today, mood: 7, sleep: 8m);                  // qualified, long
    Log(Today.AddDays(-1), mood: 5);                  // mood only — excluded
    Log(Today.AddDays(-2), sleep: 8m);                // sleep only — excluded
    Log(Today.AddDays(-3), note: "neither");          // excluded

    var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

    Assert.That(pattern.LongSleepDays, Is.EqualTo(1));
    Assert.That(pattern.ShortSleepDays, Is.EqualTo(0));
}

[Test]
public void SleepMoodPattern_buckets_at_thresholdHours_with_inclusive_lower_bound()
{
    Log(Today, mood: 7, sleep: 7m);                   // exactly threshold → long
    Log(Today.AddDays(-1), mood: 6, sleep: 6.99m);    // below threshold → short

    var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

    Assert.That(pattern.LongSleepDays, Is.EqualTo(1));
    Assert.That(pattern.ShortSleepDays, Is.EqualTo(1));
}

[Test]
public void SleepMoodPattern_is_not_confident_when_long_bucket_is_one_below_the_floor()
{
    // 4 long + 5 short (4 = MinDaysPerBucketForConfidence - 1)
    for (var i = 0; i < 4; i++)
    {
        Log(Today.AddDays(-i), mood: 7, sleep: 8m);
    }
    for (var i = 4; i < 9; i++)
    {
        Log(Today.AddDays(-i), mood: 5, sleep: 6m);
    }

    var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

    Assert.That(pattern.LongSleepDays, Is.EqualTo(4));
    Assert.That(pattern.ShortSleepDays, Is.EqualTo(5));
    Assert.That(pattern.IsConfident, Is.False);
}

[Test]
public void SleepMoodPattern_is_not_confident_when_short_bucket_is_one_below_the_floor()
{
    // 5 long + 4 short
    for (var i = 0; i < 5; i++)
    {
        Log(Today.AddDays(-i), mood: 7, sleep: 8m);
    }
    for (var i = 5; i < 9; i++)
    {
        Log(Today.AddDays(-i), mood: 5, sleep: 6m);
    }

    var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

    Assert.That(pattern.LongSleepDays, Is.EqualTo(5));
    Assert.That(pattern.ShortSleepDays, Is.EqualTo(4));
    Assert.That(pattern.IsConfident, Is.False);
}

[Test]
public void SleepMoodPattern_is_confident_when_both_buckets_meet_the_floor_exactly()
{
    // 5 long + 5 short, mood differs so we can also check the averages
    for (var i = 0; i < 5; i++)
    {
        Log(Today.AddDays(-i), mood: 8, sleep: 8m);
    }
    for (var i = 5; i < 10; i++)
    {
        Log(Today.AddDays(-i), mood: 5, sleep: 6m);
    }

    var pattern = _journal.SleepMoodPattern(days: 30, thresholdHours: 7m, now: Now);

    Assert.That(pattern.LongSleepDays, Is.EqualTo(5));
    Assert.That(pattern.ShortSleepDays, Is.EqualTo(5));
    Assert.That(pattern.LongSleepMoodAverage, Is.EqualTo(8m));
    Assert.That(pattern.ShortSleepMoodAverage, Is.EqualTo(5m));
    Assert.That(pattern.IsConfident, Is.True);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~InsightTests.SleepMoodPattern"`
Expected: FAIL — `SleepMoodPattern` type and method do not exist (compile error).

- [ ] **Step 3: Create the SleepMoodPattern value type**

Create `Kenaz.Core/Services/SleepMoodPattern.cs`:

```csharp
namespace Kenaz.Core;

/// <summary>
/// A bucket-compare of mood across nights of more vs less sleep. Carries the counts and averages
/// so the view never has to reimplement the confidence rule. Days in either bucket are *qualified
/// days* — days with both mood and sleep present.
/// </summary>
public sealed class SleepMoodPattern
{
    public SleepMoodPattern(
        decimal threshold,
        int longSleepDays,
        int shortSleepDays,
        decimal? longSleepMoodAverage,
        decimal? shortSleepMoodAverage,
        bool isConfident)
    {
        Threshold = threshold;
        LongSleepDays = longSleepDays;
        ShortSleepDays = shortSleepDays;
        LongSleepMoodAverage = longSleepMoodAverage;
        ShortSleepMoodAverage = shortSleepMoodAverage;
        IsConfident = isConfident;
    }

    public decimal Threshold { get; }
    public int LongSleepDays { get; }
    public int ShortSleepDays { get; }
    public decimal? LongSleepMoodAverage { get; }
    public decimal? ShortSleepMoodAverage { get; }
    public bool IsConfident { get; }
}
```

- [ ] **Step 4: Add constants + SleepMoodPattern method to `WellbeingJournal`**

In `Kenaz.Core/Services/WellbeingJournal.cs`, add the two constants right after the existing fields (above the constructor):

```csharp
/// <summary>The default sleep-hours threshold used by the bucket compare. View passes this in.</summary>
public const decimal DefaultSleepThresholdHours = 7m;

/// <summary>Minimum qualified days in each sleep bucket before the pattern is considered confident.</summary>
public const int MinDaysPerBucketForConfidence = 5;
```

Then add the `SleepMoodPattern` method after `WorstDay` (and before `StreakDays`):

```csharp
/// <summary>
/// A bucket-compare of mood across qualified days (mood AND sleep present) in the window, split
/// at <paramref name="thresholdHours"/> (≥ vs &lt;). Confidence requires at least
/// <see cref="MinDaysPerBucketForConfidence"/> qualified days in each bucket.
/// </summary>
public SleepMoodPattern SleepMoodPattern(int days, decimal thresholdHours, DateTimeOffset now)
{
    var today = Today(now);
    var start = today.AddDays(-(days - 1));

    var qualifiedDays = _repository.LoadAll()
        .Where(c => c.Date >= start && c.Date <= today)
        .Where(c => c.Mood.HasValue && c.Sleep.HasValue)
        .ToList();

    var longSleep = qualifiedDays.Where(c => c.Sleep!.Value >= thresholdHours).ToList();
    var shortSleep = qualifiedDays.Where(c => c.Sleep!.Value < thresholdHours).ToList();

    var longAvg = longSleep.Select(c => (decimal?)c.Mood).Average();
    var shortAvg = shortSleep.Select(c => (decimal?)c.Mood).Average();

    var isConfident = longSleep.Count >= MinDaysPerBucketForConfidence
                   && shortSleep.Count >= MinDaysPerBucketForConfidence;

    return new SleepMoodPattern(thresholdHours, longSleep.Count, shortSleep.Count, longAvg, shortAvg, isConfident);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~InsightTests.SleepMoodPattern"`
Expected: PASS (6 tests).

- [ ] **Step 6: Run the full suite to confirm nothing else regressed**

Run: `dotnet test`
Expected: PASS — all M1 + M2 + new tests green (66 total).

- [ ] **Step 7: Commit**

GitHub Desktop — message: `feat: add SleepMoodPattern bucket-compare insight`

---

### Task 3: Console UTF-8 output encoding

**Files:**
- Modify: `Kenaz.Console/Program.cs`

> Concept: Windows' default `Console.OutputEncoding` is the legacy OEM code page on older shells (classic `cmd.exe`), not UTF-8. Setting it explicitly makes `≥`, `—`, and `æøå` render correctly even on shells that haven't been switched. Modern Windows Terminal already defaults to UTF-8, so this is one-line insurance for older terminals — no behaviour change where it already worked.

- [ ] **Step 1: Add the encoding line at the top of Main**

In `Kenaz.Console/Program.cs`, change the start of `Main`:

```csharp
private static void Main()
{
    System.Console.OutputEncoding = System.Text.Encoding.UTF8;

    var repository = new JsonCheckInRepository(JsonCheckInRepository.DefaultFilePath());
```

(The `System.Console` qualification is needed because the file uses `using static System.Console;` — that brings static members into scope but not the type identifier.)

- [ ] **Step 2: Build to confirm nothing broke**

Run: `dotnet build`
Expected: 0 warnings / 0 errors.

- [ ] **Step 3: Quick smoke run**

Run: `dotnet run --project Kenaz.Console`
Expected: greeting prints normally. Type `0` to exit. No errors.

- [ ] **Step 4: Commit**

GitHub Desktop — message: `feat: set console output encoding to UTF-8`

---

### Task 4: Weekly review menu option

**Files:**
- Modify: `Kenaz.Console/Program.cs`

> Concept: `DateOnly.ToString("dddd yyyy-MM-dd", CultureInfo.CurrentCulture)` formats the weekday in the system's locale ("tirsdag" on Norwegian, "Tuesday" on English) while keeping the ISO date unambiguous. The Brightest/Hardest gating reduces to one check — `brightest.Date != hardest.Date` — which covers both "only one day in the window" and "multiple days, all equal mood" in one expression, since both cases land on the same tie-breaker.

- [ ] **Step 1: Add the menu line**

In `ShowMenu`, insert before `0) Exit`:

```csharp
        WriteLine("  6) Weekly review");
```

- [ ] **Step 2: Add the switch case and update the fallback hint**

In `Main`'s `switch`, add before `case "0":`:

```csharp
                case "6":
                    ShowWeeklyReview(journal);
                    break;
```

And update the `default` line to:

```csharp
                    WriteLine("I didn't catch that — please choose 1, 2, 3, 4, 5, 6, or 0.");
```

- [ ] **Step 3: Add the `ShowWeeklyReview` and `FormatDay` methods**

Add two new `private static` methods (e.g. after `ShowHistory`, before `ExportCheckIns`):

```csharp
private static void ShowWeeklyReview(WellbeingJournal journal)
{
    var now = DateTimeOffset.Now;
    var weekDays = journal.Last7Days(now);

    WriteLine();
    if (weekDays.Count == 0)
    {
        WriteLine("Not enough check-ins yet — your weekly review will appear here once you've logged a few days.");
        return;
    }

    WriteLine("Your week");
    WriteLine($"  Mood     avg {Average(journal.Average(c => c.Mood, 7, now))}   (last 7 days)");
    WriteLine($"  Energy   avg {Average(journal.Average(c => c.Energy, 7, now))}");
    WriteLine($"  Sleep    avg {AverageHours(journal.Average(c => c.Sleep, 7, now))}");
    WriteLine($"  {StreakMessage(journal.StreakDays(now))}");

    var brightest = journal.BestDay(c => c.Mood, 7, now);
    var hardest = journal.WorstDay(c => c.Mood, 7, now);
    if (brightest is not null && hardest is not null && brightest.Date != hardest.Date)
    {
        WriteLine();
        WriteLine($"  Brightest day  {FormatDay(brightest)}    mood {Scale(brightest.Mood)}   energy {Scale(brightest.Energy)}   sleep {Hours(brightest.Sleep)}");
        WriteLine($"  Hardest day    {FormatDay(hardest)}     mood {Scale(hardest.Mood)}   energy {Scale(hardest.Energy)}   sleep {Hours(hardest.Sleep)}");
    }

    WriteLine();
    WriteLine("A small pattern");
    var pattern = journal.SleepMoodPattern(30, WellbeingJournal.DefaultSleepThresholdHours, now);
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

private static string FormatDay(CheckIn checkIn)
{
    return checkIn.Date.ToString("dddd yyyy-MM-dd", CultureInfo.CurrentCulture);
}
```

- [ ] **Step 4: Build and verify by running**

Run: `dotnet build` — Expected: 0 warnings / 0 errors.
Run: `dotnet run --project Kenaz.Console`, choose `6`. Verify:
1. With an empty store (or a fresh `%APPDATA%\Kenaz\checkins.json`) → the "Not enough check-ins yet" message appears.
2. After checking in 2–3 days with mood + sleep → "Your week" block shows averages and streak; the pattern section says "Not enough sleep + mood data yet" (under the 5-day floor).
3. Brightest/Hardest section appears only when there are ≥ 2 distinct mood values in the last 7 days.
4. Special characters (`≥`, `—`, weekday) render correctly.

- [ ] **Step 5: Commit**

GitHub Desktop — message: `feat: add weekly review menu option`

---

### Task 5: Quiet teaser on the today-vs-week view

**Files:**
- Modify: `Kenaz.Console/Program.cs`

> Concept: the teaser is a read-only side effect appended to an existing view — no new function in Core. Direction-aware copy (`>= 1.0` vs `<= -1.0`) means the same data drives either message; the `IsConfident` gate from Core stops it firing on small N. The 1.0-point floor stops it firing on noise like a 0.2-point difference.

- [ ] **Step 1: Add the teaser at the end of `ShowTodayVsWeek`**

In `Kenaz.Console/Program.cs`, append after the `WriteLine(StreakMessage(...))` line at the end of `ShowTodayVsWeek` (just before the method's closing brace):

```csharp
        var pattern = journal.SleepMoodPattern(30, WellbeingJournal.DefaultSleepThresholdHours, now);
        if (pattern.IsConfident)
        {
            var gap = pattern.LongSleepMoodAverage!.Value - pattern.ShortSleepMoodAverage!.Value;
            if (gap >= 1.0m)
            {
                WriteLine();
                WriteLine("A small pattern: you've felt better on nights with more sleep. Open Weekly review for more.");
            }
            else if (gap <= -1.0m)
            {
                WriteLine();
                WriteLine("A small pattern: you've felt better on shorter-sleep nights. Open Weekly review for more.");
            }
        }
```

- [ ] **Step 2: Build and verify by running**

Run: `dotnet build` — Expected: 0 warnings / 0 errors.
Run: `dotnet run --project Kenaz.Console`. Verify:
1. With < 5 qualified days in either bucket (typical fresh state) → choose `2` → no teaser appears (the existing today-vs-week output is unchanged).
2. With ≥ 5 days in each bucket and a mood gap ≥ 1.0 → choose `2` → the teaser line appears beneath the streak.
3. If the gap is small (< 1.0) → no teaser.
4. To exercise both directions, you can hand-edit `%APPDATA%\Kenaz\checkins.json` to seed a few days with each sleep band and clearly different moods — relaunch the app and try option 2.

- [ ] **Step 3: Commit**

GitHub Desktop — message: `feat: surface sleep-mood pattern teaser on today-vs-week view`

---

### Task 6: README update

**Files:**
- Modify: `README.md`

> Concept: the README "what the app does" sentence is the front door — one new menu option deserves one new clause. Match the existing tone (lean, no marketing, English, comma-spliced list).

- [ ] **Step 1: Update the in-app description sentence**

In `README.md`, replace the existing sentence:

```markdown
In the app you can check in for today (mood, energy, sleep, and a note — each optional), see today against your last 7 days with a gentle streak, browse your history, and export or import your check-ins.
```

with:

```markdown
In the app you can check in for today (mood, energy, sleep, and a note — each optional), see today against your last 7 days with a gentle streak, open a weekly review (brightest and hardest day, plus a small sleep–mood pattern when there's enough data), browse your history, and export or import your check-ins.
```

- [ ] **Step 2: Build to confirm nothing broke (sanity)**

Run: `dotnet build`
Expected: 0 warnings / 0 errors.

- [ ] **Step 3: Commit**

GitHub Desktop — message: `docs: mention weekly review in the README`

---

## Verification (M3, end-to-end)

- `dotnet build` — clean (0 warnings / 0 errors).
- `dotnet test` — all NUnit tests pass (M1 + M2 + ~14 new = ~66 total).
- `dotnet run --project Kenaz.Console` manual sequence (after Tasks 4 and 5):
  1. Empty store → option 6 prints the gating message; option 2 unchanged.
  2. A few days of mixed data → option 6 shows averages + brightest/hardest (when ≥ 2 distinct moods); pattern section says "not enough yet"; option 2 has no teaser.
  3. Seed ≥ 5 days in each sleep bucket with a clear mood gap (≥ 1.0) → option 6 renders both bucket averages; option 2 shows the teaser.
  4. Flip the seed so short-sleep days have higher mood → teaser flips to "shorter-sleep nights".
  5. Special characters (`≥`, `—`, weekday) render correctly (smoke test for Task 3).
- **MVC separation check:** `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` — expected: no matches. (M3 adds zero IO to Core.)

## Self-review (plan coverage vs spec)

- Spec § "New domain (Core)" → Tasks 1 (BestDay/WorstDay) + 2 (SleepMoodPattern + constants).
- Spec § "View (Console) → Console encoding (one-time setup)" → Task 3.
- Spec § "Option 6 — Weekly review (new)" including all 4 gating rules → Task 4.
- Spec § "Option 2 — quiet teaser (enhanced)" with the ±1.0 gap and direction-aware copy → Task 5.
- Spec § Testing (~12 tests, qualified-day filter, boundary tests on both buckets) → Tasks 1 + 2 (14 tests total).
- Spec § Decisions made (windows 7/30, fixed 7 h, public constants, localized weekday + ISO, 1.0-point gap) → carried in Tasks 2, 4, 5.
- Spec § Out of scope (no Pearson, no week-over-week, no trend, no console unit tests) → honoured by the plan's absence of those tasks.
- Spec § Verification → mirrored in this plan's Verification section, plus the manual sequence in Tasks 4 and 5.
