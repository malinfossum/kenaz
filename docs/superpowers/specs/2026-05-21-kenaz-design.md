# Kenaz — Design Spec & Roadmap

A private, mobile-first daily wellbeing check-in built in C#, grown milestone by milestone.

---

## Context

Malin (career-changer, sosionom → developer) wants a *personal* C# project that is meaningful, genuinely useful to her and others, buildable and improvable over time, and impressive to a future employer. This spec is the outcome of a brainstorming session. Key decisions reached:

- **Not a rewrite of Ignite.** Ignite is her working, mid-flight, local-first ADHD task PWA (vanilla JS, IndexedDB). C# is the wrong tool for a local-first PWA, and rewriting working software kills momentum and splits focus. Ignite stays JS and keeps marching.
- **Build the one wellbeing dimension Ignite deliberately omits:** a **daily wellbeing check-in**. It complements Ignite rather than competing, and can integrate later.
- **The saturation risk is answered by the differentiator.** The wellbeing-tracker space is crowded and most apps die from passive data nobody reads (e.g. unread smartwatch stats). Kenaz's edge is **compassionate, anti-shame, social-work-informed reflection** — surfacing insight the user will actually look at and act on, which automatic trackers (watch, Flo) structurally cannot provide.
- **Success bar:** *a tool Malin actually uses + a strong fullstack C# portfolio piece.* External adoption is a bonus, not the test — so "does the world need another tracker?" does not gate the project.

**Intended outcome:** a private, mobile-first wellbeing companion built along a clear technical spine (Core → console → JSON → SQLite → ASP.NET API → PWA frontend) that ends as an impressive fullstack project tying her two project worlds together.

**Name:** **Kenaz** — the Elder Futhark torch rune, *"to spark, to bring into the light."* Fire-family sibling to Ignite ("a small flame, kept going"); verified GitHub-clean and cross-language safe. Tagline: **"bring it into the light."**

---

## Concept

A **local-first, single-user, mobile-first** daily wellbeing check-in. Quick to log (mood, energy, sleep + an optional note); over time it reflects patterns back so the user understands their days — the reflective layer a watch can't give.

## Core domain model (Core library — zero IO)

- `CheckIn`: `Date` (the user's **local** calendar date — one entry per date), `Mood` (1–10, **nullable**), `Energy` (1–10, **nullable**), `Sleep` (hours, decimal, **nullable**), `Note` (optional text), `CreatedAt` (immutable, set on first create), `UpdatedAt` (set on each edit). Optional reflective layers (emotion words, body tag) added in later milestones.
- **Required vs optional:** the three scales are the suggested minimum, but **a check-in needs only one field** (a scale or the note) to exist. A skipped scale is stored as **null, never 0** — so it can't skew insights.
- **Upsert keyed on `Date`:** logging again for a date (including backdating up to 24h) **edits that date's entry; it never duplicates**. `Date` is fixed to the local calendar date at entry time and not retroactively rewritten on DST/timezone change. An upsert where every field is empty is rejected; removing an entry is an explicit delete (not an empty edit).
- Pure domain + logic only — **no `Console`, no file/DB calls** (the "skille regler/visning/IO/lagring" discipline). **All insight functions ignore null fields** (a skipped Mood is excluded from the Mood average, not counted as 0). Pure and unit-tested.

## The payoff loop (answers the "passive data" risk)

Every interaction gives something back:
- After each check-in: **today vs last 7 days** at a glance + a **gentle, grace-aware streak** (rule in Hardening §5).
- Weekly review (later milestone): best/worst day, 7-day averages, first real insight ("better on days you slept >7h").
- Deeper correlations: later still (needs accumulated data).

## Compassionate design principles (the differentiator — bake in from the start)

- **Grace-day streaks + self-compassion on reset.** Never a shaming "0." On a break: *"You showed up 12 times this month — that matters."*
- **Flexible check-in window + backdate up to 24h.** No rigid alarm; no shame for a missed slot.
- **Optional by default.** Note and extra layers are always skippable; a check-in needs only one field. Never mandatory journaling.
- **One gentle, time-anchored reminder.** Never nags or escalates.
- **Plain, warm, first-person language.** Never clinical (no PHQ-9 tone).
- **Insight before data.** Never ship collection without the payoff loop.

These turn the documented anti-patterns (shame streaks, over-notification, data-without-payoff, positive-only bias) into explicit guardrails.

## Features by tier

**In the plan (sequenced across milestones, not all in M1):**
- Optional **emotion-word picker** (affect labeling; calm ~30-word set; pick 1–3; layered on the 1–10). Evidence-backed reflective upgrade (basis of Yale's *How We Feel*).
- **Low-day reframe prompt** — when mood/energy is very low: *"That sounds hard — what got you through today?"* Behavioral-activation, dignified (not "be grateful" on a bad day). Core social-work moat.
- **Habit-anchor** in onboarding — *"When do you usually check in?"* feeds the reminder (implementation-intention research; ADHD-friendly).
- **Goal-based onboarding** — simple default goal ("just check in daily") + optional advanced goals that tailor prompts and weekly-review emphasis. Adds a small `Goal` concept; MVP ships the default.
- **Export / import (JSON)** — data portability + privacy. Export is **versioned** (`schemaVersion`); import is **validated against the schema** (malformed/unknown records rejected or stripped) and tolerates older versions; export carries a plaintext-data warning.

**Vision / opt-in / later:**
- Optional "one good thing" micro-prompt (gratitude; skippable).
- Optional body/sensation tag (somatic awareness; trauma-informed).
- **Quiet crisis/safety resources** — discreet, always-available Norwegian helplines (e.g. Mental Helse 116 123), **bundled locally (no network)** with a review date to keep numbers current.
- **App lock (PIN/biometric)** on the mobile client.
- **Ignite → Kenaz sync** — import Ignite activity (task load) to enrich reviews ("energy dipped on your heaviest task weeks"), **via a stable Ignite export/API contract — not Ignite's internal storage** (so it doesn't break as Ignite evolves).

## Privacy

Local-first, single-user, no accounts, no telemetry. When the API arrives it stays loopback/token-guarded — never a public multi-user service.

- **Reminders stay local.** Use **local notifications while installed** (no third-party push service; nothing leaves the device). If web-push is ever introduced, payloads carry no wellbeing content and the sender is self-hosted. Reminders are best-effort in a PWA; a push server would be scope growth, flagged before adding.
- **Plaintext-at-rest is an accepted trade-off.** Local data (JSON/SQLite) and exports are unencrypted through M1–M5 — acceptable for a single-user local tool. App-lock (M6) and optional encryption-at-rest are later, opt-in. Export shows a *"this file is unencrypted — keep it somewhere private"* warning.

---

## Milestone roadmap (each shippable)

| Milestone | Delivers |
|---|---|
| **M1** | Core + console + NUnit tests + minimal JSON persistence (**atomic writes + corrupt-file recovery**) → a usable daily tool (add/edit today, history, today-vs-7-day view + streak) |
| **M2** | Repository interface formalized + JSON export/import (**validated import; plaintext-export warning**) + error handling |
| **M3** | Insights: weekly review, averages, gentle streak, first correlation (pure functions) |
| **M4** | Swap JSON → SQLite behind the same interface — **migrating existing data (never dropped); JSON kept as a backup** |
| **M5** | ASP.NET Minimal API over the data (loopback-only) |
| **M6** | Mobile-first PWA frontend (vanilla JS + design-system) consuming the API; **local-notification** reminders; goal onboarding; entry delete + undo; app-lock; crisis resources; Ignite sync (via stable contract). **Escape all user text on render (XSS).** **A11y:** accessible scale inputs (not bare sliders, ≥44px), keyboard-reachable toggles w/ `aria-pressed`, `aria-live` for conditional prompts, `prefers-reduced-motion`, design-system contrast |

The storage-behind-an-interface move (M2/M4) is the repository pattern Malin already knows from Ignite — swap implementations, domain untouched.

**Honest note:** the *carry-it-everywhere* version arrives at **M5–M6**. Until then the console is a learning + engine artifact; Malin keeps her current logging habit in the meantime.

---

## M1 scope (the immediate buildable slice)

Start by copying `_template/csharp-console-mvc/` (Core / Console / Tests; net10.0; NUnit 4.x; App.slnx). Rename `App.*` → `Kenaz.*`.

- **Kenaz.Core:**
  - `CheckIn` model with validation (scales 1–10 *when present*, nullable; Sleep ≥ 0; `Date` = local calendar date; ≥1 field required). `CreatedAt` immutable; `UpdatedAt` set on edit.
  - A journal service (e.g. `WellbeingJournal`) with `AddOrUpdate(date, …)` (**upsert keyed on date**), `GetByDate(date)`, `History()` (newest first), and pure insight helpers: `Last7Days(now)`, `Average(selector, days)` (**skips nulls**), `StreakDays(now)` (**grace rule — Hardening §5**).
  - Persistence behind `ICheckInRepository`. M1 ships a **minimal JSON file repository** (`System.Text.Json`) with **atomic write** (temp file → move/replace) and **graceful load** (on parse failure, back up the bad file and start empty — never crash). (M2 hardens it: clean interface seam, validated export/import, error handling.)
- **Kenaz.Console:**
  - Menu: 1) Check in today (mood/energy/sleep + optional note, each skippable), 2) Today vs last 7 days + streak, 3) History, 0) Exit.
  - **No delete in M1** — entries are edited (upsert), not removed. Delete + undo lands with the M6 UI.
  - Warm, plain language. Console = View; `Program.cs` orchestration = Controller-equivalent; Core = Model.
- **Kenaz.Tests (NUnit):**
  - TDD (red→green): `AddOrUpdate` (edit-not-duplicate, incl. backdated date; empty-edit rejected), `Average` (skips nulls), `Last7Days`, `StreakDays` (forgiven / broken / empty), and corrupt-file load recovery.

## Patterns / assets to reuse

- `_template/csharp-console-mvc/` scaffold (Core/Console/Tests, NUnit, `.editorconfig`, `global.json`) — copy as the starting point. Do **not** modify the template itself.
- Repository pattern (storage behind an interface) — same idea as Ignite's `src/model/*` over IndexedDB; future API/DB swap touches only the repository implementation.
- For the M6 frontend: her vanilla-JS MVC web stack + `_template/design-system/` (shared with Ignite for a consistent look). Treat `design-system/` as read-only.
- Norwegian crisis-resource list for the later milestone.

## Verification (M1, end-to-end)

- `dotnet build` clean (0 warnings / 0 errors).
- `dotnet test` — all NUnit tests pass. Each insight helper has a failing test first (TDD), then green.
- `dotnet run --project Kenaz.Console`: manually verify check-in → edit-today (no duplicate) → 7-day view + streak → history → **data persists across a restart**. Also verify: a check-in with a **skipped scale doesn't skew averages**; a **backdated entry edits the right day**; a **corrupted JSON file is recovered (backed up)** rather than crashing. Run it from Rider (or `dotnet run`) — no editor-specific launch setup needed.
- MVC separation holds: `Kenaz.Core` has zero `Console`/file calls outside the repository implementation (grep to confirm).

---

## Hardening invariants & resolved decisions (from spec stress test, 2026-05-21)

Do not simplify these away during planning/execution. The M1 plan must carry the relevant ones as explicit task acceptance criteria.

1. **Atomic writes + corrupt-file recovery** (M1/M2): write temp → atomic move/replace; on load parse failure, back up the bad file and start empty — never crash.
2. **No data loss on storage swap** (M4): migrate existing JSON into SQLite, verified, before retiring the JSON path; keep the JSON as a backup.
3. **Nullable scales + null-aware insights**: skipped scales stored as null (never 0); all insight functions exclude nulls; a check-in needs ≥1 field; an all-empty upsert is rejected.
4. **`Date` = local calendar date; upsert by date**: add/update keyed on date (backdate-safe, no duplicates); not rewritten on DST/travel; removing an entry is an explicit delete.
5. **Grace-day streak rule**: consecutive days with a check-in; **one missed day is forgiven; two consecutive misses break it**; counted against `now` (local date). Deterministically testable.
6. **Untrusted-input boundary**: validate imported/synced records against the schema (reject/strip malformed); **escape all user text** (Note, emotion, goal) at the M6 render boundary (`textContent`/escaped templating, never raw `innerHTML`); guard the JS import against prototype-pollution keys (`__proto__`). Exports carry a `schemaVersion`; import tolerates older versions (new fields default to null).
7. **Reminders**: local notifications while installed; no third-party push service; no wellbeing content leaves the device. Best-effort in a PWA; a push server is flagged scope growth.
8. **M6 a11y**: accessible scale controls (not bare sliders, ≥44px); keyboard-reachable toggles with `aria-pressed`; conditional prompts in `aria-live`; `prefers-reduced-motion`; design-system contrast.
9. **Plaintext-at-rest accepted trade-off**: unencrypted local data + exports through M1–M5; export carries the "unencrypted — keep private" warning; app-lock/encryption are later opt-in.
10. **Misc**: `CreatedAt` immutable / `UpdatedAt` on edit; Ignite sync via a stable export/API contract (not internal storage); crisis-resource numbers bundled locally with a review date.

---

## How to proceed (after approval)

1. **Scaffold** by copying `_template/csharp-console-mvc/` into `GitHub/repos/kenaz/`; rename `App.*` → `Kenaz.*`. Create the empty GitHub repo via **GitHub Desktop** (gh not authenticated; never init on GitHub side with README/LICENSE). Run `/init` + repo MEMORY.md init.
2. **Save this spec** into the repo as `docs/superpowers/specs/2026-05-21-kenaz-design.md`.
3. **Plan M1 in detail** (writing-plans), carrying the Hardening invariants as task acceptance criteria, then execute task-by-task: TDD, one task = one commit, commits via GitHub Desktop (no `Co-Authored-By`).
4. **Learning mode applies** (per her Ignite preference): walk through each new C# concept (JSON serialization, `System.Text.Json`, repository interface, test fixtures, deterministic `now`) before writing code.
