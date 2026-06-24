# Kenaz v1.0 — Standalone Android PWA — Design Spec

Rebuild Kenaz as a local-first Android PWA that runs entirely on the phone — no PC, no LAN, no API.

---

## Context

M6.1 shipped a mobile-first **web app** (`Kenaz.Web`: vanilla-JS MVC) served by the loopback `Kenaz.Api`. It works, but the phone client only works while the PC is on and reachable on the LAN. The roadmap's planned "first release" was to make that loopback app reachable from the phone (opt-in LAN-bind + PWA + `v1.0` tag).

A brainstorming session on **2026-06-24** reframed the real goal: **not "mobile access" but a phone app that does not depend on the PC at all.** PC-dependence is the friction to remove. That reframing supersedes the LAN-bind plan and the master spec's M6 model of "a PWA that *consumes* the loopback API." For the phone, the PWA becomes **standalone and local-first**: data and logic live on the device.

The C# stack (`Kenaz.Core` / `Console` / `Api`) is **kept, untouched and tested** — it remains the foundation for the later, separate **native desktop app modeled on Tidsro**, which still reuses `Core`. This milestone forks the *phone* client onto its own local-first JS path; it does not retire the C# work.

Master spec (still authoritative for domain rules + invariants): `docs/superpowers/specs/2026-05-21-kenaz-design.md`.

---

## Decisions reached (2026-06-24)

1. **Target: Android only** (Nothing Phone). iOS is deliberately not chased; a PWA stays incidentally iOS-capable for later, at no extra cost.
2. **Architecture: standalone PWA, not native MAUI.** Reuse the existing web UI; put data + logic on the phone. Chosen over MAUI because it fits Malin's web-dev focus, reuses the already-polished UI, reaches daily use fastest, and — via GitHub Pages — sidesteps HTTPS/cert friction. Accepted cost: the domain logic now lives in two languages (see Invariant N1).
3. **Store: IndexedDB.** The existing data layer is already async (HTTP) and the controller already `await`s it, so an async IndexedDB store drops into the same shape — and the token/network error routing *simplifies away* (no token, no "unreachable"). Request persistent storage so Android won't evict it.
4. **Offline: full, by default.** All data + logic are local, so the app works with no network and with the PC off. "Offline" is the normal state, not a special case. No offline-sync queue is built (it would be throwaway once a true standalone app owns the data).
5. **Host: GitHub Pages.** The repo (`malinfossum/kenaz`) is already public, so serving the static shell adds zero new exposure. Code is public; check-in data never leaves the phone.
6. **Migration + backup: export/import, no server.** One-time: export JSON from the PC app → import on the phone (reuses the existing versioned envelope + newer-wins merge). Ongoing backup and any cross-device move = export-to-file from the phone. There is no sync server, by design.
7. **Domain ported to JS, pinned by tests.** Port the insights + merge logic from `Core`; re-express the C# behavior tests in JS (Vitest — a new dev dependency) so both languages are pinned to the same cases.
8. **Scope: feature parity with the M6.1 web app, minus the plumbing.** Drop the HTTP layer, bearer token, and LAN-bind (none are needed standalone). Deliver incrementally — the daily check-in loop first, insights second — but `v1.0` = full parity.
9. **Release: tag `v1.0`** at parity, with release notes describing the standalone PWA (optionally a GitHub Release on the public repo).

---

## Architecture

**Reuse as-is**
- The whole View (`src/view/screens/*` — except the Setup token screen, see below — the design-system, `src/styles/main.css`) and the model + MVC shell (`main.js` / `app.js` / `model.js`). The contract that keeps the View untouched: the new store + domain emit the **same `CheckInResponse` / `InsightsResponse` shapes** the View already renders. *Two additive exceptions* for the new Data screen (below): `view.js` gains one tab + route, and `model.js` gains a `dataResult` field — the existing screens and their rendering are untouched.

**Replace — the transport seam becomes a local seam**
- `src/api.js` (the only module that calls `fetch` / touches the token) → **`src/store.js`**, an IndexedDB-backed store for check-in reads/writes: `getCheckIns()` (all, newest first), `putCheckIn(date, data)`, `deleteCheckIn(date)`. (There is no single-date get — the controller loads all and finds today client-side, unchanged.)
- The server-computed `getInsights()` moves **client-side** into `domain/insights.js` (see below) — this is the real substance of the port.
- **Removed (no server to talk to):** the token methods (`hasToken` / `setToken` / `clearToken`), the **Setup token screen**, the `unauthorized` / `unreachable` error paths, and the matching model state (`needsSetup`, `setupError`, `connection`). The controller's `refresh()` collapses to "load check-ins → compute insights → set data."

**New — domain (`src/domain/`)**
- `insights.js` — what now produces the `InsightsResponse`: 7-day averages (null-skipping), gentle grace-day streak, best/worst day, sleep–mood pattern, confidence gating. Ported from `WellbeingJournal` / `InsightsService`.
- `merge.js` — newer-wins merge used by import. Ported from the existing merge.

**New — Data screen (export/import) — required by the standalone backup/migration story (the M6.1 web app had none)**
- `src/view/screens/data.js` + a 4th "Data" tab/route in `view.js` and a `dataResult` field in `model.js`. Export downloads the PascalCase envelope; import parses + merges a backup. Carries the Invariant #9 unencrypted-file warning and an "export regularly — your only backup" nudge. Import is keyboard-operable (a real button proxies a hidden file input).

**New — PWA**
- `public/manifest.webmanifest` (name, icons, `display: standalone`, theme/background colors).
- A service worker that caches the app shell + assets so the app installs and launches offline (navigations network-first so deploys propagate; only successful responses cached — see N4).
- App icons; `<link rel="manifest">` + `theme-color` meta wired into `index.html`.

**IndexedDB shape**
- One object store `checkins`, `keyPath: "date"` (the local calendar date `yyyy-MM-dd` as key — preserves Invariant #4's upsert-by-date). Records carry the existing fields including `createdAt` / `updatedAt` (needed by merge). Schema versioned via `onupgradeneeded`; a small `meta` entry tracks the store version. Call `navigator.storage.persist()` on first run.

**Build + deploy**
- Vite builds the static shell (no longer into `Kenaz.Api/wwwroot`; `vite.config.js` retargets to a normal `dist/` with the correct GitHub Pages `base` path). Deploy `dist/` to GitHub Pages. The dev API proxy is removed (no API to proxy).

---

## Invariants carried forward (from the master spec — still apply)

These remain acceptance criteria; the JS port must honor them:

- **#3 Nullable scales + null-aware insights** — skipped scales stored as null (never 0); the ported JS insights must exclude nulls; a check-in needs ≥1 field; an all-empty upsert is rejected.
- **#4 `Date` = local calendar date; upsert by date** — IndexedDB key is the date; editing a date never duplicates; removing is an explicit delete.
- **#5 Grace-day streak rule** — one missed day forgiven, two consecutive misses break it; counted against the local `now`; deterministically testable.
- **#6 Untrusted-input boundary** — validate imported records against the schema (reject/strip malformed); **escape all user text on render** (`textContent` / escaped templating, never raw `innerHTML`); guard the JS import against prototype-pollution keys (`__proto__`). Export carries `schemaVersion`; import tolerates older versions.
- **#8 A11y** — already in the shipped UI; preserved (accessible scale controls ≥44px, `aria-pressed` toggles, `aria-live` prompts, `prefers-reduced-motion`, design-system contrast).
- **#9 Plaintext-at-rest accepted** — IndexedDB data is unencrypted on the phone (Android app-sandboxed, same single-user caveat); export carries the "unencrypted — keep private" warning.

## New invariants / resolved decisions for this milestone

- **N1 — Domain lives in two languages; manage drift by tests.** `Core` stays canonical for the desktop track; the JS `domain/` is pinned to the *same* behavior cases (ported C# tests). When domain rules change, both must change together.
- **N2 — The phone is the source of truth.** No server dependency; no automatic cloud backup. Durability rests on `navigator.storage.persist()` plus the user-controlled export-to-file backstop. *Accepted residual risk:* if persistence isn't granted (likelier before install), the browser could evict IndexedDB under storage pressure — installed Android PWAs are normally granted persistence, and the Data screen nudges regular exports as the backstop.
- **N3 — Public host serves code only.** No check-in data ever leaves the device. (Strengthens privacy vs M5–M6.1: there is no loopback token to leak anymore.) *Named trade-off:* the host (GitHub) logs request metadata — that the app was fetched, with IP/time/user-agent — never check-in data; the service worker cuts post-install fetches. Accepted for installability.
- **N4 — Offline is the default, not a feature.** The service worker caches the shell so the app loads with no network; data was already local. No offline-write sync queue is built. *Cache strategy:* navigations are network-first (so new deploys propagate) with the cached shell as the offline fallback; assets are cache-first and only successful (`response.ok`) responses are cached, so a transient error is never cached and cannot brick the install.

---

## Verification

- **Vitest (domain):** ported `insights` + `merge` reproduce the C# test cases (red → green during the port). Null-skipping, streak grace rule, and merge newer-wins explicitly covered.
- **Install:** open the GitHub Pages URL on the Nothing Phone → "Install app" → launches standalone from the home screen.
- **On-device manual loop:** check in → edit today (no duplicate) → today vs 7 days + streak → weekly review + sleep–mood pattern → history edit → export (shows the unencrypted-file warning) → import. Then **airplane mode: app still fully works**, and **PC powered off: app unaffected**. Relaunch/reinstall → data persists.
- **Migration:** export from the PC app → import on the phone → history matches.
- **Discipline:** MVC separation preserved (View renders, no logic; domain is pure); all user text escaped on render; import guarded against malformed/`__proto__` records.

---

## How to proceed (after approval)

1. **Save this spec** in the repo (already at this path). Commit via **GitHub Desktop** per Malin's workflow (no `Co-Authored-By`) — not auto-committed.
2. **Plan in detail** (writing-plans), carrying the carried-forward + new invariants as task acceptance criteria, sequenced **daily-loop-first**. New dev dependencies (Vitest; possibly the tiny `idb` helper) are flagged for approval at plan time.
3. **Execute task-by-task:** TDD for the domain port, one task = one commit (GitHub Desktop). UI stays verify-by-running on the phone.
4. **Learning mode applies:** walk through each new concept before coding — IndexedDB + `onupgradeneeded`, the service-worker lifecycle, Vite + GitHub Pages `base` paths, and Vitest.
