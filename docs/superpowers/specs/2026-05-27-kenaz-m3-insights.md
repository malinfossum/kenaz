# Kenaz M3 — Insights: Weekly Review + First Correlation

A reflective layer that closes the payoff loop: a dedicated weekly review and the project's first real correlation insight, both pure functions in Core.

---

## Context

M1 shipped the daily tool (check-in, history, today-vs-week glance, gentle streak). M2 shipped data portability (versioned export/import with newer-wins merge). M3 is the **insight tier** from the original design spec ([2026-05-21-kenaz-design.md](2026-05-21-kenaz-design.md), milestone table line 82): *"Insights: weekly review, averages, gentle streak, first correlation (pure functions)."*

Reading that line against the M1 code: averages (`WellbeingJournal.Average`), streak (`WellbeingJournal.StreakDays`), and the 7-day window (`WellbeingJournal.Last7Days`) already shipped — and option 2 ("Today vs your last 7 days") already renders averages + streak. So M3's net-new work is narrower than the milestone line suggests:

1. A **weekly review** view distinct from the after-check-in glance — sit-with-it framing with brightest/hardest day callouts.
2. The **first correlation** insight the original spec hints at (line 38): *"better on days you slept >7h."*

The payoff loop (original spec line 36-38) splits these as two moments: *after each check-in* (glance) vs *weekly review* (deeper). M3 honours that split by adding a new menu option for the deeper view and keeping option 2 light, with one quiet teaser line that links to the new view when a confident pattern emerges.

**Intended outcome:** the user feels their week reflected back warmly, sees concrete brightest/hardest days, and — once enough data exists — gets one honest pattern ("on nights you slept ≥7 h, your mood averaged X vs Y") that an automatic tracker structurally cannot give them.

---

## Concept

Two surfaces, one purpose: **show the user something honest they couldn't have noticed themselves.**

- **Option 6 (new): Weekly review** — a sit-with-it report covering the last 7 days. Summary averages, brightest/hardest day, the sleep↔mood pattern when it's confident, gentle gating messages otherwise.
- **Option 2 (enhanced): today vs week** — unchanged glance, plus a one-line teaser at the bottom *only* when the pattern is confident *and* the bucket-average gap is meaningful (≥ 1.0 mood points).

No statistical machinery, no Pearson, no trend lines. The original spec's anti-pattern is *"data without payoff"* — M3 ships the payoff, not the dashboard.

---

## New domain (Core)

Three new methods on `WellbeingJournal`, matching the existing helpers' shape (selector + window + injected clock):

```csharp
public CheckIn? BestDay (Func<CheckIn, decimal?> selector, int days, DateTimeOffset now);
public CheckIn? WorstDay(Func<CheckIn, decimal?> selector, int days, DateTimeOffset now);
public SleepMoodPattern SleepMoodPattern(int days, decimal thresholdHours, DateTimeOffset now);

public const decimal DefaultSleepThresholdHours = 7m;
public const int MinDaysPerBucketForConfidence = 5;
```

- `BestDay` / `WorstDay`: window-bounded, ignores nulls on the selected field, returns null when nothing qualifies. Tie-breaker: **most recent date wins** (so consecutive equal days don't surface a stale day).
- `SleepMoodPattern`: looks back `days` days, considers only days with **both** mood and sleep present (call these *qualified days*), splits them at `thresholdHours` (≥ vs <), returns counts + averages + `IsConfident`. The per-bucket day floor counts qualified days only — a user with 10 sleep-only and 10 mood-only days still has 0 qualified days.

One new public value type in `Kenaz.Core/Services/SleepMoodPattern.cs`:

```csharp
public sealed class SleepMoodPattern
{
    public decimal Threshold { get; }
    public int LongSleepDays { get; }
    public int ShortSleepDays { get; }
    public decimal? LongSleepMoodAverage { get; }
    public decimal? ShortSleepMoodAverage { get; }
    public bool IsConfident { get; }   // both buckets ≥ MinDaysPerBucketForConfidence (averages are guaranteed non-null by the qualified-day filter)
}
```

The constants live on `WellbeingJournal` so they're discoverable from one place. `IsConfident` is computed inside the journal and carried on the result — the view never has to reimplement the gate.

**Why not an `Insights` class or a `WeeklyReview` aggregate yet:** three helpers don't earn either. Same call as M2's `Merge` going on the journal. The journal currently sits at 158 lines; M3 brings it to roughly ~210. Split when it crosses ~250.

---

## View (Console)

### Console encoding (one-time setup in `Program.Main`)

Before the greeting, set `Console.OutputEncoding = System.Text.Encoding.UTF8;`. M3 introduces `≥` (U+2265) in the pattern copy; the existing `—` and Norwegian weekdays (`æøå`) also benefit. Windows Terminal defaults to UTF-8 in modern Windows — this is one-line insurance for older shells.

### Option 6 — Weekly review (new)

```
Your week
  Mood     avg 6.8   (last 7 days)
  Energy   avg 5.9
  Sleep    avg 7.2 h
  Streak   5 days — lovely consistency.

  Brightest day  tirsdag 2026-05-26    mood 9   energy 7   sleep 8.5 h
  Hardest day    fredag 2026-05-22     mood 3   energy 4   sleep 5.0 h

A small pattern
  On nights you slept ≥7 h, your mood averaged 7.1 — vs 5.4 on shorter nights.
  (last 30 days: 12 longer-sleep days, 8 shorter)
```

Weekday is localized via `CurrentCulture` so Norwegian users see Norwegian weekdays; the ISO date stays unambiguous.

**Gating, in order:**
1. *No check-ins in last 7 days* → "Not enough check-ins yet — your weekly review will appear here once you've logged a few days." (Return.)
2. *No day in the window has a mood value* → Brightest/Hardest section silently skipped.
3. *Fewer than 2 distinct mood values in the window* → Brightest/Hardest skipped. Covers both the single-day case and the all-equal-mood case (without this, the "most recent date wins" tie-breaker would surface the same date for both lines, which reads as a bug).
4. *`SleepMoodPattern.IsConfident == false`* → "Not enough sleep + mood data yet — keep logging both and the pattern will show up here."

### Option 2 — quiet teaser (enhanced)

`ShowTodayVsWeek` runs exactly as before. After the streak message, IF `pattern.IsConfident` AND `|LongAvg − ShortAvg| ≥ 1.0`, append one direction-aware line:

```
A small pattern: you've felt better on nights with more sleep. Open Weekly review for more.
```

(or `"shorter-sleep nights"` when the gap reverses)

The 1.0-point gap stops the line firing for noise like a 0.2-point difference. No emoji.

### Copy principles (from the original spec)

- **"Brightest / Hardest"** rather than "best / worst" — compassionate framing for the hard day.
- **All gating is forward-looking** ("once you've logged a few days") — never shaming.
- **Pattern output names its window inline** so the user knows what it's based on.
- **First-person warm** — "your week", "you've felt better" — never clinical.

---

## Decisions made (change either if you disagree)

| Decision | Choice | Why |
|---|---|---|
| Scope | Spec-baseline: weekly review + first correlation | Matches milestone line 82; averages/streak already shipped |
| Correlation form | Bucket compare + best-day storytelling | Mix of stats + concrete narrative; warmer than either alone |
| UX placement | Full view in option 6 + teaser in option 2 | Honours the spec's "after check-in" vs "weekly review" split |
| Window | 7 days for review, 30 days for correlation | Each window fits its purpose; correlation needs more N |
| Architecture | Extend `WellbeingJournal` | Continues M1/M2 pattern; split later if file crosses ~250 lines |
| Constants | Public on the journal | One discoverable place to tune the threshold and gate |
| Date format | Localized weekday + ISO ("tirsdag 2026-05-26") | Personal touch + unambiguous date |
| Teaser gap | ≥ 1.0 mood points | Notable on the 1–10 scale; dodges noise |
| Confidence gate | ≥ 5 days in each sleep bucket | "Working week" floor; testable; revisitable |

---

## Out of scope (explicit cuts)

- **Pearson correlation / statistical significance.** "First correlation" is the bucket compare; deeper stats wait for accumulated data (original spec line 38).
- **Week-over-week comparison.** Cut during scope selection.
- **Trend call-outs** ("sleep trending up the last 3 days"). Cut during scope selection.
- **Other correlation pairings** (sleep↔energy, mood↔energy). Ship one first insight.
- **Personalized threshold** (user's median sleep). Fixed 7 h via `DefaultSleepThresholdHours`.
- **Auto-firing weekly review** (e.g., on Sunday). Manual menu option only.
- **Console unit tests.** Matches M1/M2 convention — `Program.cs` verified by running.

---

## Testing

All new tests follow the existing `InsightTests.cs` fixture (`InMemoryCheckInRepository`, fixed `Now`, `Log(...)` helper). **~12 new tests, bringing total to ~64.**

**`BestDay` / `WorstDay`** (6 tests):
- Null on empty window
- Null when no day in window has the selected field
- Picks the day with the max / min value on the selector
- Ignores days where the selector is null
- Tie → most recent date wins
- Respects window boundary (sixth day in, seventh day out)

**`SleepMoodPattern`** (6 tests):
- Empty window → counts 0, averages null, not confident
- Excludes days missing either sleep or mood (only qualified days counted)
- Buckets correctly at `thresholdHours` (≥ vs <)
- `IsConfident` false when `LongSleepDays = MinDaysPerBucketForConfidence − 1`
- `IsConfident` false when `ShortSleepDays = MinDaysPerBucketForConfidence − 1`
- `IsConfident` true when both buckets meet the floor exactly

---

## Verification (M3, end-to-end)

- `dotnet build` — clean (0 warnings / 0 errors).
- `dotnet test` — all NUnit tests pass.
- `dotnet run --project Kenaz.Console` — manual:
  1. Empty store → option 6 shows the gating message; option 2 unchanged.
  2. A few days of data → option 6 shows averages + brightest/hardest; pattern section says "not enough yet"; option 2 has no teaser.
  3. ≥ 5 days in each sleep bucket with a clear gap → pattern section renders both averages; option 2 shows the teaser.
  4. Pattern gap reversed → teaser flips to "shorter-sleep nights".
- **MVC separation check:** `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` — expected: no matches. (M3 adds zero IO to Core.)

---

## Self-review (spec coverage vs original milestone line 82)

- Weekly review → option 6 with averages + brightest/hardest + correlation.
- Averages → already shipped; reused as the summary in option 6.
- Gentle streak → already shipped; reused as the closing line of the summary.
- First correlation (pure functions) → `SleepMoodPattern` + the `BestDay`/`WorstDay` storytelling.

---

## How to proceed (after approval)

1. Hand off to **writing-plans** to produce `docs/superpowers/plans/2026-05-27-kenaz-m3-insights.md` with TDD task-by-task steps, carrying the gating rules and constants as explicit acceptance criteria.
2. Execute task-by-task: TDD, one task = one commit, commits via GitHub Desktop (no `Co-Authored-By`).
3. **Learning mode applies** (per the original spec): each task carries a short `> Concept:` note on the new C# idea before the code.
