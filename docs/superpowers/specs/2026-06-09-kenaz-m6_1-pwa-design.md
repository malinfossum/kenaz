# Kenaz M6.1 — Desktop PWA over the Loopback API

The client half of *carry-it-everywhere*. M5 built the loopback API; M6.1 builds the web app that consumes it — a **desktop-only, single-machine** app that reads and writes check-ins and renders insights, **served same-origin by `Kenaz.Api`**. It adds one Core service (`InsightsService`, which finally lifts the insight *gating* out of the console View into a single source), one read-only endpoint (`GET /insights`), a new vanilla-JS MVC web project (`Kenaz.Web`), and a small console refactor so the console and the web app share that one gating source. Installability and offline are deliberately deferred to M6.2.

---

## Context

M1 shipped the daily tool. M2 added portable export/import. M3 layered insight functions onto `WellbeingJournal` (averages, streak, best/worst day, sleep–mood pattern) — and put the *gating* for those insights (when is a pattern confident enough to show? when are two days distinct enough to call one "brightest"?) inline in the console View. M4 swapped the live store to SQLite. M5 built the loopback HTTP API.

M5's own spec named this milestone as its other half ([2026-05-31-kenaz-m5-api-design.md](2026-05-31-kenaz-m5-api-design.md) line 11: *"M6's PWA is the client that will consume this contract"*), and it deliberately deferred two things to here:

- **Insight endpoints** — *"Added when M6's PWA actually renders them, so the logic stays single-sourced in Core rather than guessed-at now."* ([m5 line 249](2026-05-31-kenaz-m5-api-design.md))
- **CORS** — *"CORS for M6's PWA origin is a deliberate M6 decision, made when that origin exists."* ([m5 line 218](2026-05-31-kenaz-m5-api-design.md))

Both come due now, and both resolve *smaller* than M5 anticipated: the insight endpoint is a single bundled `GET /insights`, and CORS disappears entirely because the PWA is served from the **same origin** as the API.

**The slice.** "M6 = the PWA" is too big for one spec, so it is cut in two:

- **M6.1 (this spec)** — insight endpoint + a full **online** read+write PWA. Requires the API running; loads over loopback HTTP.
- **M6.2 (later)** — installability and offline: `manifest.webmanifest`, a service worker, and a caching strategy. Everything that makes it work *without* the server reachable.

**Desktop-only, single machine.** The loopback + token stance from M5 is unchanged. "Carry it everywhere" here means *an app that feels installed on her own desktop* — not a phone, not multi-device, not a hosted service. Those are explicitly rejected.

**Intended outcome:** with `Kenaz.Api` running, opening `http://127.0.0.1:5247` shows the app. First run asks for the token (paste once). Then: check in for today on a form, see a last-7-days glance, browse and edit history, and read a weekly review whose insights are computed by the same Core code the console uses.

---

## Concept

Four moving parts, each with one job:

1. **`InsightsService` (Core, new)** — composes `WellbeingJournal`'s existing public read methods into one `InsightsSummary`, and **owns the gating** that currently lives in the console View. This is the M3-flagged "split the journal as it nears ~250 lines" (`WellbeingJournal` is 241 today) done as a *responsibility* split, not a line-count one: the journal keeps computing, the service decides what is *showable*. No change to `WellbeingJournal`.
2. **`GET /insights` (Api, new)** — one bundled, **read-only** endpoint behind the existing bearer filter, returning the whole summary (computed values + gating flags) as one DTO.
3. **`Kenaz.Web` (new project)** — a vanilla-JS MVC app from the `web-vite` template. An `api.js` fetch wrapper is the single seam to the server; the Model holds state, the View renders and escapes, the Controller wires them.
4. **Console refactor** — `ShowTodayVsWeek` and `ShowWeeklyReview` stop computing gating inline and render from `InsightsService` instead. This is what makes the gating *single-sourced* rather than duplicated; the copy stays in each View.

Same-origin serving means the browser treats the API as its own origin — no CORS, and the token (not cookies) is the only credential, so there is nothing for a cross-site request to forge.

---

## Architecture

```
Kenaz.Web/                         (NEW — Vite + Biome, vanilla JS, from _template/web-vite)
  index.html                       app shell: <main id="main"> + bottom-tab <nav>
  vite.config.js                   build.outDir → ../Kenaz.Api/wwwroot ; dev proxy /checkins,/insights → 127.0.0.1:5247
  src/
    main.js                        boots the app
    app.js                         wires createModel / createView / createController
    api.js                         fetch wrapper — owns token, JSON, 401 routing, error classification (the ONLY caller of fetch)
    model.js                       app state (active tab, check-ins, insights, token-present); subscribe/notify; no DOM, no fetch
    view.js                        renders the active screen from state; data-action + bindActions; escapes user text; no logic
    controller.js                  handles actions → calls api.js → updates model; wires model+view
    screens/ today.js history.js review.js setup.js   (per-screen render helpers the View delegates to)
    styles/main.css                project-specific only (dark-/mobile-first); design-system tokens consumed read-only
  design-system/                   (read-only, bundled by the template)

Kenaz.Core/Services/               (NEW files alongside WellbeingJournal / SleepMoodPattern)
  InsightsService.cs               Summarize(now) → InsightsSummary ; composes the journal ; owns gating
  InsightsSummary.cs               value type: week averages, streak, highlights, embedded SleepMoodPattern, teaser gate

Kenaz.Api/
  Program.cs                       + InsightsService DI ; + UseDefaultFiles/UseStaticFiles + MapFallbackToFile ; + /insights group
  Endpoints/InsightsEndpoints.cs   GET /insights → InsightsResponse (read-only — no WriteLock)
  Contracts/InsightsResponse.cs    wire shape of InsightsSummary
  wwwroot/                         build output of Kenaz.Web (git-ignored — derived artifact)

Kenaz.Console/Program.cs           ShowTodayVsWeek / ShowWeeklyReview render from InsightsService (gating moves out; copy stays)
```

**MVC framing holds, twice.** In C#, `Kenaz.Core` is the Model; the console and the API are transport adapters over it; `InsightsService` is a Core service that adds no IO (the M3/M4 separation grep `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` still returns nothing). In JS, `Kenaz.Web` is its own MVC: Model = state, View = render, Controller = behavior, and `api.js` is the transport seam — the JS mirror of the repository pattern (the Model never fetches, exactly as Core's Model never touches a file outside `Storage/`).

---

## New domain (Core)

### `InsightsService` — composes the journal, owns the gating

The gating currently sits in two console methods. Read directly from the live code, the rules are:

- **Sleep–mood teaser** (the one-line nudge on "Today vs week", `Program.cs:149-163`): show only when `pattern.IsConfident` **and** `|LongAvg − ShortAvg| ≥ 1.0`. Direction depends on the sign of the gap (`≥ 1.0` → more sleep felt better; `≤ −1.0` → shorter nights felt better).
- **Brightest/hardest** (`Program.cs:208`): show only when `brightest` and `hardest` both exist **and** `brightest.Date != hardest.Date`. (Because ties break to the most recent date, equal dates mean fewer than two *distinct* mood values in the window — so this flag is exactly "≥ 2 distinct moods".)
- **Pattern card** (the full long-vs-short numbers on "Weekly review", `Program.cs:217-228`): show whenever `pattern.IsConfident` (no `≥ 1.0` gate — that gate is only for the teaser).
- **Whole-section gate**: both screens bail when `Last7Days(now).Count == 0`.

`InsightsService` takes the journal and computes a summary for a given `now`, using the **same windows the console uses**: 7-day for averages / streak / best-worst (mood), 30-day at `DefaultSleepThresholdHours` for the pattern. `now` is a **method parameter**, not an injected clock — it mirrors the journal's read-side methods and makes the gating boundaries trivial to test with a fixed clock.

```csharp
public sealed class InsightsService
{
    private const int WeekDays = 7;
    private const int PatternDays = 30;

    private readonly WellbeingJournal _journal;
    public InsightsService(WellbeingJournal journal) => _journal = journal;

    public InsightsSummary Summarize(DateTimeOffset now)
    {
        var hasWeekData = _journal.Last7Days(now).Count > 0;

        var brightest = _journal.BestDay(c => c.Mood, WeekDays, now);
        var hardest   = _journal.WorstDay(c => c.Mood, WeekDays, now);
        var hasHighlights = brightest is not null && hardest is not null && brightest.Date != hardest.Date;

        var pattern = _journal.SleepMoodPattern(PatternDays, WellbeingJournal.DefaultSleepThresholdHours, now);
        var (showTeaser, direction) = TeaserFrom(pattern);   // IsConfident && |gap| >= 1.0, signed

        return new InsightsSummary(
            moodAverage:   _journal.Average(c => c.Mood,   WeekDays, now),
            energyAverage: _journal.Average(c => c.Energy, WeekDays, now),
            sleepAverage:  _journal.Average(c => c.Sleep,  WeekDays, now),
            streakDays:    _journal.StreakDays(now),
            hasWeekData:   hasWeekData,
            brightestDay:  hasHighlights ? brightest : null,
            hardestDay:    hasHighlights ? hardest   : null,
            hasHighlights: hasHighlights,
            sleepMood:     pattern,
            showSleepTeaser: showTeaser,
            teaserDirection: direction);
    }
}
```

`Average`/`BestDay`/`WorstDay`/`SleepMoodPattern`/`StreakDays`/`Last7Days` are all **existing, already-tested** journal methods — `InsightsService` adds no new computation, only composition and the gating decisions.

### `InsightsSummary` — the value type

```csharp
public sealed class InsightsSummary
{
    // 7-day glance (null = not enough data for that metric)
    public decimal? MoodAverage { get; }
    public decimal? EnergyAverage { get; }
    public decimal? SleepAverage { get; }
    public int StreakDays { get; }
    public bool HasWeekData { get; }            // any check-in in the last 7 days

    // 7-day mood highlights (null unless HasHighlights)
    public CheckIn? BrightestDay { get; }
    public CheckIn? HardestDay { get; }
    public bool HasHighlights { get; }

    // 30-day sleep–mood pattern (reuses the existing M3 value type)
    public SleepMoodPattern SleepMood { get; }

    // Today-screen teaser gate (derived from the pattern)
    public bool ShowSleepTeaser { get; }
    public SleepTeaserDirection TeaserDirection { get; }   // None / MoreSleepBetter / LessSleepBetter
}
```

The View (console or web) reads these flags and supplies the **words**. It never re-derives a threshold. `SleepMood` carries the existing `Threshold / LongSleepDays / ShortSleepDays / LongSleepMoodAverage / ShortSleepMoodAverage / IsConfident`, so the Review pattern card renders straight from it.

---

## API — `GET /insights`

One endpoint, mirroring the M5 wiring exactly: a `/insights` route group with the **same** `BearerTokenFilter`, returning one DTO. **Read-only — it does not take the `WriteLock`.** No query parameters.

```csharp
// Program.cs additions
builder.Services.AddSingleton(sp => new InsightsService(sp.GetRequiredService<WellbeingJournal>()));

app.MapGroup("/insights")
   .AddEndpointFilter<BearerTokenFilter>()
   .MapInsightsEndpoints();
```

```csharp
// Endpoints/InsightsEndpoints.cs
public static RouteGroupBuilder MapInsightsEndpoints(this RouteGroupBuilder group)
{
    group.MapGet("/", (InsightsService insights) =>
        Results.Ok(InsightsResponse.From(insights.Summarize(DateTimeOffset.Now))));
    return group;
}
```

| Method | Route | Maps to | Success | Errors |
|---|---|---|---|---|
| `GET` | `/insights` | `insights.Summarize(now)` | `200` `InsightsResponse` | `401` no/bad token (group filter) |

### `InsightsResponse` (DTO, in `Kenaz.Api`)

The wire shape projects domain types to JSON-friendly ones (dates as `yyyy-MM-dd`, the enum as a string, highlights slimmed to what the cards show — no note):

```csharp
public record DayHighlight(string Date, int? Mood, int? Energy, decimal? Sleep);

public record InsightsResponse(
    decimal? MoodAverage, decimal? EnergyAverage, decimal? SleepAverage,
    int StreakDays, bool HasWeekData,
    DayHighlight? BrightestDay, DayHighlight? HardestDay, bool HasHighlights,
    decimal SleepThreshold, int LongSleepDays, int ShortSleepDays,
    decimal? LongSleepMoodAverage, decimal? ShortSleepMoodAverage, bool SleepPatternConfident,
    bool ShowSleepTeaser, string TeaserDirection)
{
    public static InsightsResponse From(InsightsSummary s) => /* flatten s + s.SleepMood; dates → yyyy-MM-dd */;
}
```

DTOs live in the API (the wire contract is the API's concern); Core's models stay clean — the same split M5 used for `CheckInResponse`.

---

## Console refactor (single-sourcing the gating)

`ShowTodayVsWeek` and `ShowWeeklyReview` change from *computing* gating to *rendering* an `InsightsSummary`:

- Build an `InsightsService` over the same journal (in `Program.Main`, beside the journal) and call `Summarize(DateTimeOffset.Now)` once per screen.
- Replace the inline `pattern.IsConfident && gap >= 1.0m` / `brightest.Date != hardest.Date` / `Last7Days(now).Count == 0` checks with the summary's `ShowSleepTeaser` + `TeaserDirection` / `HasHighlights` / `HasWeekData` flags.
- **The console's wording is unchanged** — only *where the decision is made* moves. Acceptance is "console output is identical before and after", confirmed by running (the console has no automated render tests, by convention).

This deletes the duplicated rules; after it, the gating exists in exactly one place (`InsightsService`), consumed by both the console and the web app.

---

## The web app (`Kenaz.Web`)

A vanilla-JS MVC app copied from `_template/web-vite`, `design-system/` consumed **read-only**, **dark-mode-first and mobile-first**, accessibility throughout. Navigation is a **bottom tab bar**: Today · History · Review. Setup is shown automatically when there is no token (first run) or after a 401.

**Layers (strict, per the template's `createModel/createView/createController`):**
- **`api.js`** — the only place that calls `fetch`. Owns: attaching `Authorization: Bearer <token>` from localStorage, JSON encode/decode, and classifying every outcome into a clean signal (ok / unauthorized / validation / not-found / server-error / unreachable). Returns data or throws a typed error the Controller handles.
- **Model** — app state only: active tab, the check-ins list, the latest `InsightsSummary`, and whether a token is present. Subscribe/notify. No DOM, no `fetch`.
- **View** — renders the active screen from state; forwards events through `data-action` + `bindActions`; **escapes all user-supplied text** (see Security). No logic, no fetch.
- **Controller** — handles actions, calls `api.js`, updates the Model, swaps screens. The only layer that knows about both.

**Screens:**
1. **Today** — *form-first*. Mood and Energy are **range sliders** (1–10), Sleep is a number input (0–24, accepts `7` or `7.5`), Note is a textarea. **Save** → `PUT /checkins/{today}` → then a **"Last 7 days" glance card** (mood/energy/sleep averages + streak, straight from `GET /insights`). If today already has a check-in, the form opens pre-filled (edit-in-place, the console's behavior).
2. **History** — compact rows, newest first: date · a note snippet · `M/E/S` values. Tapping a row opens the **same check-in form** bound to that date (edit → `PUT /checkins/{date}`). An **✕** deletes it (`DELETE /checkins/{date}`) **after a small confirm** — delete is new to the user here (the console never had it), so a guard prevents accidental loss.
3. **Weekly review** — a summary card (7-day averages + streak), **brightest / hardest** side by side (only when `HasHighlights`), and a **sleep↔mood pattern** card (the long-vs-short numbers when `SleepPatternConfident`, else the warm "not enough yet" line). All gating comes from the `/insights` flags; the View only chooses words.
4. **Setup** — a single field to paste the token (shown in the API's startup banner, or in `%APPDATA%\Kenaz\api-token`), stored to localStorage. One-time; re-shown only on 401.

The check-in form is one reusable component used by both Today and History-edit; client-side validation **mirrors the `CheckIn` rules** (at least one of mood/energy/sleep/note; mood/energy 1–10; sleep 0–24) for instant feedback, with the server's `400` as the backstop.

---

## Serving & dev

- **Production (same-origin).** `npm run build` in `Kenaz.Web` emits to `Kenaz.Api/wwwroot`. `Kenaz.Api` adds `UseDefaultFiles()` + `UseStaticFiles()` to serve the shell and assets, and `MapFallbackToFile("index.html")` so any non-API path renders the SPA. The `/checkins` and `/insights` groups are mapped *before* the fallback, so API routes win and everything else falls through to the app. The static shell and assets are **not** behind the bearer filter — they hold no secrets, and the Setup screen must load *before* a token exists; only `/checkins` and `/insights` require the token.
- **Dev (Vite proxy).** `vite dev` serves the app with HMR on its own port and **proxies** `/checkins` and `/insights` to `http://127.0.0.1:5247`. The client always uses **relative** URLs, so the same code is same-origin in production and proxied in dev — no environment switch, and still no CORS.
- **`wwwroot` is a derived artifact** — git-ignored (alongside the existing `.gitignore` edits). Run flow is "build the web app, then `dotnet run --project Kenaz.Api`"; dev uses Vite. An MSBuild target that runs `npm run build` automatically is a nice-to-have, deferred (it couples the C# build to npm — unnecessary for a single dev machine).

---

## Security & privacy

- **Same-origin, no CORS.** The PWA is served by the API, so requests are same-origin. The API answers no CORS headers and needs none. M5's deferred CORS decision resolves to *"none"* — the cleanest possible outcome.
- **No cookies, no ambient credential.** The only credential is the bearer token the client attaches explicitly. There is nothing a cross-site request could ride on, so the CSRF property M5 noted holds for free and CORS is not load-bearing for it.
- **Output escaping — the new boundary (invariant #6, output side).** The console rendered to a terminal; the PWA renders **notes (free text) into the DOM**. All user-supplied text is written via `textContent` / safe DOM construction — **never** `innerHTML` with user data. This is the milestone's main new attack surface and the View owns it.
- **Token in localStorage — the accepted trade-off.** localStorage gives paste-once persistence (the chosen UX) and is readable by same-origin JS. On a loopback, single-user desktop where the token already sits plaintext in `%APPDATA%\Kenaz\api-token` (invariant #9), localStorage adds no exposure *at rest*. The real risk it introduces is XSS exfiltration — which is exactly why the output-escaping rule above is non-negotiable. On `401`, the token is cleared and Setup is re-shown.
- **App shell is public; data is guarded.** Static files and the fallback are unauthenticated (the shell is not secret); `/checkins` and `/insights` stay behind the constant-time bearer filter. A `401` from either routes the user to Setup.
- **Unchanged from M5:** loopback bind (`127.0.0.1` + `[::1]`), HTTP (traffic never leaves the machine), bodyless `500` on storage failure, request logging pinned to `Warning`, plaintext-at-rest (invariant #9). `/insights` is read-only and exposes only derived aggregates — no new data leaves Core that the check-in endpoints didn't already.

---

## Decisions made (change either if you disagree)

| Decision | Choice | Why |
|---|---|---|
| Scope of M6.1 | Online read+write PWA **+** `GET /insights`; **no** offline/installability | "M6 = the PWA" is too big for one spec. Online first; M6.2 adds manifest/service-worker/caching. |
| Platform | Desktop-only, single machine | Loopback + token stance from M5 unchanged. Phone/multi-device/hosting rejected (local-first). |
| Serving | Same-origin — `Kenaz.Web` builds into `Kenaz.Api/wwwroot`, served by the API | Kills CORS entirely; relative URLs; the "installed app" feel. |
| Dev | Vite dev server proxying `/checkins` + `/insights` → `127.0.0.1:5247` | HMR while developing; same relative-URL client code as production; still no CORS. |
| Insight API | One bundled, read-only `GET /insights` returning values **and** gating flags | Granular per-metric routes would duplicate gating in JS. One round-trip, one source of truth. |
| Query params | **None** (dropped from the earlier sketch) | No M6.1 screen changes the window or threshold; both use the fixed M3 defaults (7-day, 30-day, 7 h). Add when a screen needs them. |
| Gating home | New `InsightsService` in Core owns it; **console refactored to consume it** | The point of the service is *single source*. Leaving the console's inline copy would duplicate, not lift. |
| `WellbeingJournal` | **Unchanged** | The service composes its existing public methods; the journal keeps computing. |
| `now` | A method parameter on `Summarize(now)`, not an injected clock | Mirrors the journal's read methods; makes gating-boundary tests trivial with a fixed clock. |
| Auth (client) | Paste-once token → localStorage → `Authorization: Bearer`; `401` → clear + Setup | Simplest credential for a static single token; XSS risk mitigated by output escaping. |
| Web stack | Vanilla-JS MVC from `web-vite`; `api.js` as the only `fetch` seam | Matches the user's MVC discipline; `api.js` is the JS mirror of the repository pattern. |
| UI | Bottom tab bar; form-first Today; mood/energy sliders; dark-/mobile-first; a11y | Approved in the wireframe pass; matches the design-system and the user's standards. |
| Delete UX | Small confirm before `DELETE` | Delete is new to the user (console never had it); a guard prevents accidental data loss. |
| `wwwroot` | Git-ignored derived artifact; build then run; MSBuild auto-build deferred | Build output isn't source; auto-npm-in-C#-build is needless coupling for one machine. |
| DTO | `InsightsResponse` + `DayHighlight` in `Kenaz.Api`, flattened, dates as `yyyy-MM-dd` | Wire contract is the API's concern; mirrors `CheckInResponse`. |
| Frontend tests | Verify-by-running (no JS test runner added) | Project convention; the logic-heavy parts (gating, validation, persistence) are tested in Core/API. |

---

## Out of scope (explicit cuts)

- **Offline, service worker, `manifest.webmanifest`, installability.** The whole of M6.2. M6.1 requires the server reachable.
- **Export / import over HTTP.** Console-only; `JsonCheckInArchive` already covers portability.
- **CORS.** Moot under same-origin serving.
- **Multi-device, phone, accounts, multi-user, hosting beyond loopback.** Against the local-first single-user stance.
- **`?days` / `?threshold` query parameters.** No consumer in M6.1.
- **Insight *write*/configuration.** Insights are read-only derived values.
- **A JS test framework (Vitest, etc.).** Verify-by-running, per convention.
- **`WellbeingJournal` changes, repository/interface changes, schema changes.** None needed.

---

## Testing

`InsightsService` is the one piece with real new logic, so it gets the most coverage; `GET /insights` gets the M5 `WebApplicationFactory` treatment; the console refactor and the whole frontend are verified by running. **~18 new tests, bringing the total from 117 to ~135.**

### `InsightsServiceTests` (Core, ~13)
Construct over an in-memory/seeded repository + journal; call `Summarize(fixedNow)`; assert flags at their boundaries.
- `Empty_store_has_no_week_data_and_null_averages`
- `Week_averages_compute_over_the_7_day_window`
- `Streak_passes_through_from_the_journal`
- `Highlights_available_when_two_distinct_moods`
- `Highlights_gated_when_all_moods_equal` (brightest.Date == hardest.Date → `HasHighlights` false, both null)
- `Highlights_gated_with_a_single_mood_day`
- `Sleep_teaser_shown_when_confident_and_gap_is_exactly_one` (boundary)
- `Sleep_teaser_hidden_when_gap_below_one`
- `Sleep_teaser_hidden_when_not_confident_despite_large_gap`
- `Sleep_teaser_direction_is_more_sleep_better_when_long_avg_higher`
- `Sleep_teaser_direction_is_less_sleep_better_when_short_avg_higher`
- `Pattern_confident_carries_threshold_counts_and_averages`
- `Pattern_not_confident_below_min_days_per_bucket`

### `InsightsApiTests` (Api, ~5)
Same factory/config-seam pattern as `CheckInApiTests` (per-test temp DB, known token, `ClearAllPools()` in teardown).
- `Get_insights_without_token_returns_401`
- `Get_insights_with_wrong_token_returns_401`
- `Get_insights_on_empty_store_returns_200_with_no_data_flags`
- `Get_insights_with_seeded_data_returns_computed_values_and_flags`
- `Put_then_get_insights_reflects_the_new_checkin` (read-through over the live store)

### Console refactor — verify-by-running
Run the console before/after; confirm "Today vs week" and "Weekly review" output is **identical**. No new automated tests (convention: composition roots and console rendering are verified by running).

### Frontend — verify-by-running
Build `Kenaz.Web` → `wwwroot`, run `Kenaz.Api`, open the app, and walk every path (see Verification). `api.js` (token handling, 401 routing, error classification) is the highest-logic piece, so it gets the most deliberate hand-exercise: bad token → Setup; server stopped → banner.

---

## Verification (M6.1, end-to-end)

- `dotnet build Kenaz.slnx` — clean (0/0).
- `dotnet test Kenaz.slnx` — all NUnit tests pass (~135).
- **MVC separation check:** `rg -n "Console\.|File\.|Directory\.|Path\." Kenaz.Core --glob '!Kenaz.Core/Storage/*'` — no matches (`InsightsService` is pure composition).
- **Console parity:** run `Kenaz.Console`, compare "Today vs week" + "Weekly review" output against pre-refactor — identical.
- **Web, hermetic via the M5 config seam (no real data touched):** run `Kenaz.Api` against a throwaway DB/token, build `Kenaz.Web`, open the printed URL, and confirm:
  1. First load with no stored token → **Setup**; pasting the wrong token → still Setup (`401`).
  2. Paste the real token → app loads; **Today** form saves → glance card shows averages + streak.
  3. **History** lists the check-in; edit changes it; ✕ (after confirm) removes it.
  4. **Review** shows the section once there's week data; with seeded data, brightest/hardest and the pattern card appear; with thin data, the gated "not enough yet" copy appears.
  5. Stop the API mid-session → the calm **"Can't reach Kenaz"** banner; restart + retry recovers.
  6. A note containing `<script>`/HTML renders as literal text (output-escaping check).
- **a11y spot-check:** tab through the bottom nav and the form; sliders have labels; contrast comes from design-system tokens.

---

## Self-review (coverage vs the roadmap)

- *Carry-it-everywhere, client half* → an installed-feeling desktop web app over the M5 API; M6.2 finishes the "everywhere" with offline/installability.
- *Insights rendered* → M5's deferred insight endpoint lands here as `GET /insights`, single-sourced through `InsightsService` exactly as M5 promised ("so the logic stays single-sourced in Core rather than guessed-at now").
- *Single source of truth* → gating lifted out of the console View into `InsightsService`, consumed by both clients; `WellbeingJournal` untouched.
- *Local-first, single-user* → loopback + token unchanged; same-origin removes CORS; desktop-only.

---

## How to proceed (after approval)

1. Hand off to **writing-plans**. Given the milestone's size and clean backend/frontend seam, the plan is split in two: `docs/superpowers/plans/2026-06-09-kenaz-m6_1-backend.md` (`InsightsService` + `/insights` + console refactor) and `docs/superpowers/plans/2026-06-09-kenaz-m6_1-web.md` (the `Kenaz.Web` PWA). Carry as explicit acceptance criteria across them: the `InsightsService` gating rules (matching the console exactly), the `200`/`401` `/insights` contract, the console-parity requirement, same-origin serving + Vite dev proxy, the paste-once/`401`-reprompt auth flow, the error/empty-state behaviors, and output escaping at the render boundary.
2. Execute task-by-task: TDD for Core (`InsightsService`) and Api (`/insights`); verify-by-running for the console refactor and the whole frontend. One task = one commit, commits via GitHub Desktop (no `Co-Authored-By`).
3. **Learning-mode notes** per task on the new ideas: Vite build/dev + proxy, vanilla-JS MVC wiring, a `fetch` wrapper as a transport seam, `localStorage` auth, ASP.NET static files + `MapFallbackToFile` SPA hosting, range-slider + form a11y, and DOM output escaping.
4. **README update** is part of the plan: a short "Desktop app (M6.1)" note — build `Kenaz.Web`, run `Kenaz.Api`, open the loopback URL, paste the token once.
