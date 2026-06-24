# Kenaz v1.0 — Standalone Android PWA — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the existing `Kenaz.Web` thin client into a fully standalone, installable Android PWA — data in IndexedDB, insights computed locally in JS, served from GitHub Pages — that works with the PC off.

**Architecture:** Keep the existing vanilla-JS MVC View untouched. Replace the HTTP transport seam (`src/api.js`) with an IndexedDB store (`src/store.js`) and a ported JS domain (`src/domain/`) that reproduces the C# insights + newer-wins merge. Wire the controller to those instead of the API; drop the token/Setup/offline machinery. Add a PWA manifest + a hand-rolled service worker, a minimal export/import Data screen (the only backup + migration path), and a GitHub Actions → Pages deploy.

**Tech Stack:** Vanilla JS (ES modules), Vite (build + `base` for Pages), IndexedDB, a hand-rolled service worker, Vitest (new dev dependency — pre-approved in the spec) for the pure domain, GitHub Actions + Pages for hosting.

**Workflow conventions (apply to every task):**
- **TDD** for the pure domain (`src/domain/**`): write the failing Vitest test, see it fail, implement, see it pass.
- The IndexedDB store, the service worker, the Data screen, and install are **verify-by-running** (on the Nothing Phone / in Chrome), not unit-tested.
- **One task = one commit, via GitHub Desktop.** Never add a `Co-Authored-By` trailer or Claude attribution. Commit steps below give the message only.
- **Learning-mode:** before the first task that introduces each new concept (IndexedDB/`onupgradeneeded`, the service-worker lifecycle, Vite + GitHub Pages `base`, Vitest), walk Malin through it before writing code.
- Run all JS tests with `npm test` (from `Kenaz.Web/`). The global Stop hook also runs `dotnet test` — the C# stays green because it's untouched.

---

## Reference A — the two object shapes the domain/store must emit (camelCase)

The View reads these exact property names. Do not rename.

**`CheckIn` record** (one element of `state.checkIns`, and `state.today`; also the IndexedDB row):
```
{ date: "yyyy-MM-dd", mood: number|null, energy: number|null,
  sleep: number|null, note: string|null,
  createdAt: ISOstring, updatedAt: ISOstring }
```
`mood`/`energy` are integers 1–10 or null; `sleep` is 0–24 (0.5 steps) or null. `createdAt`/`updatedAt` are carried + persisted but the View never reads them. `getCheckIns()` returns these **newest-first** (descending `date`).

**`Insights` object** (`state.insights`, computed locally) — exactly 16 fields:
```
{ moodAverage: number|null, energyAverage: number|null, sleepAverage: number|null,
  streakDays: number,
  hasWeekData: boolean,
  brightestDay: {date,mood,energy,sleep}|null,   // NO note
  hardestDay:   {date,mood,energy,sleep}|null,   // NO note
  hasHighlights: boolean,
  sleepThreshold: number,
  longSleepDays: number, shortSleepDays: number,
  longSleepMoodAverage: number|null, shortSleepMoodAverage: number|null,
  sleepPatternConfident: boolean,
  showSleepTeaser: boolean,
  teaserDirection: "None"|"MoreSleepBetter"|"LessSleepBetter" }
```

## Reference B — domain constants (ported from C#)

| Constant | Value | Source |
|---|---|---|
| `WEEK_DAYS` | 7 | week glance + highlights window |
| `PATTERN_DAYS` | 30 | sleep–mood pattern window |
| `SLEEP_THRESHOLD` | 7 | long/short split, inclusive on long side (`sleep >= 7` = long) |
| `MIN_DAYS_PER_BUCKET` | 5 | confidence needs ≥5 in **each** bucket |
| `TEASER_GAP` | 1.0 | teaser shows when `|longAvg − shortAvg| ≥ 1.0` |

## Reference C — export envelope (PascalCase, schemaVersion 1)

Matches the C# `JsonCheckInArchive` exactly so files interchange with the C# app:
```json
{ "SchemaVersion": 1, "ExportedAt": "<ISO>",
  "CheckIns": [ { "Date": "yyyy-MM-dd", "Mood": 7, "Energy": 6, "Sleep": 7.5,
                  "Note": "ok", "CreatedAt": "<ISO>", "UpdatedAt": "<ISO>" } ] }
```
Import accepts this (and tolerates camelCase defensively). Only a **newer** `SchemaVersion` is rejected; equal/older is read.

## File structure

| File | Responsibility | Action |
|---|---|---|
| `Kenaz.Web/src/domain/constants.js` | the table in Reference B | create |
| `Kenaz.Web/src/domain/dates.js` | local-date helpers (today, add-days, window) | create |
| `Kenaz.Web/src/domain/checkin.js` | `validateCheckIn` (canonical validator) | create |
| `Kenaz.Web/src/domain/insights.js` | averages, streak, best/worst, sleep–mood, `computeInsights` | create |
| `Kenaz.Web/src/domain/merge.js` | newer-wins merge | create |
| `Kenaz.Web/src/domain/archive.js` | export envelope + import parse/validate | create |
| `Kenaz.Web/src/domain/*.test.js` | Vitest ports of the C# cases | create |
| `Kenaz.Web/src/store.js` | IndexedDB store (replaces `api.js`) | create |
| `Kenaz.Web/src/pwa.js` | SW registration + persistence request | create |
| `Kenaz.Web/src/controller/controller.js` | wire to store + domain; drop token/offline | rewrite |
| `Kenaz.Web/src/api.js` | obsolete transport seam | delete |
| `Kenaz.Web/src/view/screens/data.js` | minimal export/import screen | create |
| `Kenaz.Web/src/view/view.js` | add the "Data" tab + route | modify |
| `Kenaz.Web/index.html` | manifest link + theme-color | modify |
| `Kenaz.Web/public/manifest.webmanifest` | PWA manifest | create |
| `Kenaz.Web/public/sw.js` | hand-rolled service worker | create |
| `Kenaz.Web/public/icons/*` | 192/512/maskable PNGs | create |
| `Kenaz.Web/vite.config.js` | `base`, `dist` outDir, drop proxy | modify |
| `Kenaz.Web/package.json` | Vitest dep + test scripts | modify |
| `.github/workflows/deploy.yml` | build + deploy to Pages | create |

`model.js`, `app.js`'s wiring, all other `view/screens/*`, `utils/format.js`, `utils/dom.js`, `main.js` and the whole `public/design-system/**` stay **untouched**. `setup.js` stays on disk but is never reached (the controller never sets `needsSetup`), so the View needs no edit for its removal.

---

## Phase A — Pure domain port (TDD with Vitest)

### Task A1: Add Vitest and the domain folder

**Files:**
- Modify: `Kenaz.Web/package.json`
- Create: `Kenaz.Web/src/domain/constants.js`
- Create: `Kenaz.Web/src/domain/smoke.test.js`

- [ ] **Step 1: Install Vitest** (pre-approved in the spec)

Run (in `Kenaz.Web/`): `npm install -D vitest`
Expected: `vitest` appears under `devDependencies`; `package-lock.json` is created/updated (commit it — the deploy workflow needs it).

- [ ] **Step 2: Add test scripts** — edit `package.json` `scripts` so `test` runs Vitest:

```json
"scripts": {
	"dev": "vite",
	"build": "vite build",
	"preview": "vite preview",
	"test": "vitest run",
	"test:watch": "vitest",
	"format": "biome format --write .",
	"format:check": "biome format .",
	"lint": "biome lint .",
	"check": "biome check --write ."
}
```

- [ ] **Step 3: Create the constants**

`src/domain/constants.js`:
```js
export const WEEK_DAYS = 7
export const PATTERN_DAYS = 30
export const SLEEP_THRESHOLD = 7
export const MIN_DAYS_PER_BUCKET = 5
export const TEASER_GAP = 1.0
```

- [ ] **Step 4: Add a smoke test** — `src/domain/smoke.test.js`:
```js
import { expect, test } from "vitest"
import { WEEK_DAYS } from "./constants.js"

test("vitest runs and constants load", () => {
	expect(WEEK_DAYS).toBe(7)
})
```

- [ ] **Step 5: Run it** — `npm test` → Expected: 1 passing test.

- [ ] **Step 6: Commit (GitHub Desktop)** — `chore(M7-pwa): add Vitest + domain constants`

---

### Task A2: Local-date helpers (`dates.js`)

Every window/streak is **local-calendar-date** based. We operate on `"yyyy-MM-dd"` strings; lexicographic order equals chronological order, so window membership is a string compare. Day arithmetic uses a UTC tuple purely as an integer day index (no timezone crossing, DST-safe).

**Files:**
- Create: `Kenaz.Web/src/domain/dates.js`
- Test: `Kenaz.Web/src/domain/dates.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/dates.test.js`:
```js
import { expect, test } from "vitest"
import { addDaysIso, todayIso, withinWindow } from "./dates.js"

test("todayIso uses LOCAL date components", () => {
	const now = new Date(2026, 4, 22, 9, 0, 0) // local 2026-05-22 09:00 (month 4 = May)
	expect(todayIso(now)).toBe("2026-05-22")
})

test("addDaysIso subtracts and crosses month boundaries", () => {
	expect(addDaysIso("2026-05-22", -6)).toBe("2026-05-16")
	expect(addDaysIso("2026-05-01", -1)).toBe("2026-04-30")
	expect(addDaysIso("2026-05-22", -1)).toBe("2026-05-21")
})

test("withinWindow is inclusive of today and the (days-1)th day back", () => {
	const today = "2026-05-22"
	expect(withinWindow("2026-05-22", today, 7)).toBe(true) // today
	expect(withinWindow("2026-05-16", today, 7)).toBe(true) // today-6
	expect(withinWindow("2026-05-15", today, 7)).toBe(false) // today-7
	expect(withinWindow("2026-05-23", today, 7)).toBe(false) // future
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/dates.test.js` → FAIL (module not found).

- [ ] **Step 3: Implement** — `src/domain/dates.js`:
```js
function pad(n) {
	return String(n).padStart(2, "0")
}

/** Local "today" as yyyy-MM-dd (local date components, never UTC). */
export function todayIso(now = new Date()) {
	return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

/** Add (or subtract) whole days to a yyyy-MM-dd string. DST-safe integer day math. */
export function addDaysIso(iso, delta) {
	const [y, m, d] = iso.split("-").map(Number)
	const t = new Date(Date.UTC(y, m - 1, d))
	t.setUTCDate(t.getUTCDate() + delta)
	return `${t.getUTCFullYear()}-${pad(t.getUTCMonth() + 1)}-${pad(t.getUTCDate())}`
}

/** True when `iso` is within the `days`-long window ending at (and including) `today`. */
export function withinWindow(iso, today, days) {
	const start = addDaysIso(today, -(days - 1))
	return iso >= start && iso <= today
}
```

- [ ] **Step 4: Run to verify it passes** — `npx vitest run src/domain/dates.test.js` → PASS.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): local-date helpers for windows + streak`

---

### Task A3: Check-in validation (`checkin.js`)

Mirrors C# `CheckIn.Validate` verbatim (same messages). Becomes the single validator used by the controller (replacing its inline copy) and by import.

**Files:**
- Create: `Kenaz.Web/src/domain/checkin.js`
- Test: `Kenaz.Web/src/domain/checkin.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/checkin.test.js`:
```js
import { expect, test } from "vitest"
import { validateCheckIn } from "./checkin.js"

test("accepts a single scale field", () => {
	expect(validateCheckIn({ mood: 5, energy: null, sleep: null, note: null })).toBeNull()
})
test("accepts note-only", () => {
	expect(validateCheckIn({ mood: null, energy: null, sleep: null, note: "just a note" })).toBeNull()
})
test("accepts scale boundaries 1 and 10, sleep 0 and 24", () => {
	expect(validateCheckIn({ mood: 1, energy: 10, sleep: null, note: null })).toBeNull()
	expect(validateCheckIn({ mood: null, energy: null, sleep: 0, note: null })).toBeNull()
	expect(validateCheckIn({ mood: null, energy: null, sleep: 24, note: null })).toBeNull()
})
test("rejects mood below 1 / above 10", () => {
	expect(validateCheckIn({ mood: 0, energy: null, sleep: null, note: null })).toMatch(/Mood/)
	expect(validateCheckIn({ mood: 11, energy: null, sleep: null, note: null })).toMatch(/Mood/)
})
test("rejects sleep negative / over 24", () => {
	expect(validateCheckIn({ mood: null, energy: null, sleep: -1, note: null })).toMatch(/Sleep/)
	expect(validateCheckIn({ mood: null, energy: null, sleep: 25, note: null })).toMatch(/Sleep/)
})
test("rejects all-empty", () => {
	expect(validateCheckIn({ mood: null, energy: null, sleep: null, note: null })).toMatch(/at least one/)
})
test("rejects whitespace-only note with no scales", () => {
	expect(validateCheckIn({ mood: null, energy: null, sleep: null, note: "   " })).toMatch(/at least one/)
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/checkin.test.js` → FAIL.

- [ ] **Step 3: Implement** — `src/domain/checkin.js`:
```js
/** Returns an error message string, or null when valid. Mirrors C# CheckIn.Validate. */
export function validateCheckIn({ mood, energy, sleep, note }) {
	if (mood != null && (mood < 1 || mood > 10)) return "Mood must be between 1 and 10 when provided."
	if (energy != null && (energy < 1 || energy > 10)) return "Energy must be between 1 and 10 when provided."
	if (sleep != null && (sleep < 0 || sleep > 24)) return "Sleep must be between 0 and 24 hours when provided."
	if (mood == null && energy == null && sleep == null && !(note && note.trim())) {
		return "A check-in needs at least one of: mood, energy, sleep, or a note."
	}
	return null
}
```

- [ ] **Step 4: Run to verify it passes** — `npx vitest run src/domain/checkin.test.js` → PASS.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): canonical check-in validator`

---

### Task A4: Averages, window list, and the streak grace rule (`insights.js` part 1)

**Files:**
- Create: `Kenaz.Web/src/domain/insights.js`
- Test: `Kenaz.Web/src/domain/insights.streak.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/insights.streak.test.js`:
```js
import { expect, test } from "vitest"
import { addDaysIso } from "./dates.js"
import { average, last7Days, streakDays } from "./insights.js"

const NOW = new Date(2026, 4, 22, 9, 0, 0) // local 2026-05-22 09:00
const TODAY = "2026-05-22"
const back = (n) => addDaysIso(TODAY, -n)
const rec = (date, fields = {}) => ({
	date, mood: null, energy: null, sleep: null, note: null,
	createdAt: "2026-05-22T09:00:00Z", updatedAt: "2026-05-22T09:00:00Z", ...fields,
})

// averages skip nulls, return null on empty
test("average skips nulls", () => {
	expect(average([8, 4])).toBe(6)
})
test("average returns null on empty", () => {
	expect(average([])).toBeNull()
})

// last-7 window edges
test("last7Days includes today and today-6, excludes today-7", () => {
	expect(last7Days([rec(TODAY)], NOW)).toHaveLength(1)
	expect(last7Days([rec(back(6))], NOW)).toHaveLength(1)
	expect(last7Days([rec(back(7))], NOW)).toHaveLength(0)
})

// streak grace rule
test("streak is 0 on empty", () => {
	expect(streakDays([], NOW)).toBe(0)
})
test("streak counts consecutive logged days", () => {
	expect(streakDays([rec(TODAY), rec(back(1)), rec(back(2))], NOW)).toBe(3)
})
test("streak does not reset when today not yet logged", () => {
	expect(streakDays([rec(back(1)), rec(back(2))], NOW)).toBe(2)
})
test("streak forgives a single gap", () => {
	expect(streakDays([rec(TODAY), rec(back(2)), rec(back(3))], NOW)).toBe(3)
})
test("streak breaks on two consecutive gaps", () => {
	expect(streakDays([rec(TODAY), rec(back(3))], NOW)).toBe(1)
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/insights.streak.test.js` → FAIL.

- [ ] **Step 3: Implement** — `src/domain/insights.js` (part 1; the rest is appended in A5/A6):
```js
import { WEEK_DAYS } from "./constants.js"
import { addDaysIso, todayIso, withinWindow } from "./dates.js"

/** Mean of an array of numbers; null on empty. Caller filters out null fields first. */
export function average(values) {
	if (values.length === 0) return null
	return values.reduce((a, b) => a + b, 0) / values.length
}

/** Check-ins within a `days`-long window ending today, newest first. */
export function windowed(checkIns, now, days) {
	const today = todayIso(now)
	return checkIns
		.filter((c) => withinWindow(c.date, today, days))
		.sort((a, b) => b.date.localeCompare(a.date))
}

export function last7Days(checkIns, now) {
	return windowed(checkIns, now, WEEK_DAYS)
}

/** 7-day mean of one field, nulls skipped, null when the window has no value. */
export function averageField(checkIns, now, field, days = WEEK_DAYS) {
	const values = windowed(checkIns, now, days)
		.map((c) => c[field])
		.filter((v) => v != null)
	return average(values)
}

/** Consecutive logged days; one one-day gap forgiven, two in a row break it. */
export function streakDays(checkIns, now) {
	const today = todayIso(now)
	const logged = new Set(checkIns.map((c) => c.date).filter((d) => d <= today))
	if (logged.size === 0) return 0

	let cursor = [...logged].sort().at(-1) // most recent logged day <= today
	let streak = 0
	while (true) {
		if (logged.has(cursor)) {
			streak++
			cursor = addDaysIso(cursor, -1)
		} else if (logged.has(addDaysIso(cursor, -1))) {
			cursor = addDaysIso(cursor, -1) // forgive a single gap (no increment)
		} else {
			break
		}
	}
	return streak
}
```

- [ ] **Step 4: Run to verify it passes** — `npx vitest run src/domain/insights.streak.test.js` → PASS.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): 7-day averages + streak grace rule`

---

### Task A5: Brightest/hardest day + sleep–mood pattern (`insights.js` part 2)

**Files:**
- Modify: `Kenaz.Web/src/domain/insights.js` (append)
- Test: `Kenaz.Web/src/domain/insights.pattern.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/insights.pattern.test.js`:
```js
import { expect, test } from "vitest"
import { addDaysIso } from "./dates.js"
import { bestDay, sleepMoodPattern, worstDay } from "./insights.js"

const NOW = new Date(2026, 4, 22, 9, 0, 0)
const TODAY = "2026-05-22"
const back = (n) => addDaysIso(TODAY, -n)
const rec = (date, fields = {}) => ({
	date, mood: null, energy: null, sleep: null, note: null,
	createdAt: "2026-05-22T09:00:00Z", updatedAt: "2026-05-22T09:00:00Z", ...fields,
})

test("bestDay picks max mood; null when window empty or no mood", () => {
	expect(bestDay([], NOW)).toBeNull()
	expect(bestDay([rec(TODAY, { note: "x" })], NOW)).toBeNull()
	const best = bestDay([rec(TODAY, { mood: 5 }), rec(back(1), { mood: 9 }), rec(back(2), { mood: 3 })], NOW)
	expect(best.date).toBe(back(1))
})
test("bestDay ignores null-mood days and the 7th day back", () => {
	expect(bestDay([rec(TODAY, { mood: 5 }), rec(back(1), { note: "x" }), rec(back(2), { mood: 7 })], NOW).date).toBe(back(2))
	expect(bestDay([rec(back(7), { mood: 9 }), rec(back(1), { mood: 3 })], NOW).date).toBe(back(1))
})
test("bestDay tie-breaks to the most recent date", () => {
	expect(bestDay([rec(back(3), { mood: 7 }), rec(back(1), { mood: 7 }), rec(back(2), { mood: 7 })], NOW).date).toBe(back(1))
})
test("worstDay picks min mood; tie-breaks to most recent", () => {
	expect(worstDay([rec(TODAY, { mood: 5 }), rec(back(1), { mood: 3 }), rec(back(2), { mood: 9 })], NOW).date).toBe(back(1))
	expect(worstDay([rec(back(3), { mood: 2 }), rec(back(1), { mood: 2 }), rec(back(2), { mood: 2 })], NOW).date).toBe(back(1))
})

test("sleep–mood pattern: empty window → zeros and nulls", () => {
	const p = sleepMoodPattern([], NOW)
	expect(p).toMatchObject({ longSleepDays: 0, shortSleepDays: 0, longSleepMoodAverage: null, shortSleepMoodAverage: null, isConfident: false })
})
test("pattern excludes days missing sleep or mood", () => {
	const p = sleepMoodPattern([
		rec(TODAY, { mood: 7, sleep: 8 }), rec(back(1), { mood: 5 }),
		rec(back(2), { sleep: 8 }), rec(back(3), { note: "x" }),
	], NOW)
	expect(p.longSleepDays).toBe(1)
	expect(p.shortSleepDays).toBe(0)
})
test("pattern buckets at threshold with inclusive long side (7 = long, 6.99 = short)", () => {
	const p = sleepMoodPattern([rec(TODAY, { mood: 7, sleep: 7 }), rec(back(1), { mood: 6, sleep: 6.99 })], NOW)
	expect(p.longSleepDays).toBe(1)
	expect(p.shortSleepDays).toBe(1)
})
test("pattern not confident when a bucket is one below the floor of 5", () => {
	const checkIns = []
	for (let i = 0; i < 4; i++) checkIns.push(rec(back(i), { mood: 7, sleep: 8 })) // 4 long
	for (let i = 4; i < 9; i++) checkIns.push(rec(back(i), { mood: 5, sleep: 6 })) // 5 short
	const p = sleepMoodPattern(checkIns, NOW)
	expect(p).toMatchObject({ longSleepDays: 4, shortSleepDays: 5, isConfident: false })
})
test("pattern confident when both buckets meet 5 exactly, with averages", () => {
	const checkIns = []
	for (let i = 0; i < 5; i++) checkIns.push(rec(back(i), { mood: 8, sleep: 8 }))
	for (let i = 5; i < 10; i++) checkIns.push(rec(back(i), { mood: 5, sleep: 6 }))
	const p = sleepMoodPattern(checkIns, NOW)
	expect(p).toMatchObject({ longSleepDays: 5, shortSleepDays: 5, longSleepMoodAverage: 8, shortSleepMoodAverage: 5, isConfident: true })
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/insights.pattern.test.js` → FAIL.

- [ ] **Step 3: Implement** — append to `src/domain/insights.js`:
```js
import { MIN_DAYS_PER_BUCKET, PATTERN_DAYS, SLEEP_THRESHOLD } from "./constants.js"

/** Day with the extreme mood in the 7-day window. dir: "max" | "min". Tie → most recent date. */
function extremeDay(checkIns, now, dir) {
	const candidates = last7Days(checkIns, now).filter((c) => c.mood != null)
	if (candidates.length === 0) return null
	candidates.sort((a, b) => {
		if (a.mood !== b.mood) return dir === "max" ? b.mood - a.mood : a.mood - b.mood
		return b.date.localeCompare(a.date) // tie: most recent first
	})
	return candidates[0]
}

export function bestDay(checkIns, now) {
	return extremeDay(checkIns, now, "max")
}
export function worstDay(checkIns, now) {
	return extremeDay(checkIns, now, "min")
}

/** 30-day sleep→mood comparison, bucketed at SLEEP_THRESHOLD (inclusive long side). */
export function sleepMoodPattern(checkIns, now) {
	const qualified = windowed(checkIns, now, PATTERN_DAYS).filter((c) => c.mood != null && c.sleep != null)
	const long = qualified.filter((c) => c.sleep >= SLEEP_THRESHOLD)
	const short = qualified.filter((c) => c.sleep < SLEEP_THRESHOLD)
	return {
		threshold: SLEEP_THRESHOLD,
		longSleepDays: long.length,
		shortSleepDays: short.length,
		longSleepMoodAverage: average(long.map((c) => c.mood)),
		shortSleepMoodAverage: average(short.map((c) => c.mood)),
		isConfident: long.length >= MIN_DAYS_PER_BUCKET && short.length >= MIN_DAYS_PER_BUCKET,
	}
}
```
> Move the existing `import { WEEK_DAYS } from "./constants.js"` line at the top of the file into one combined import: `import { MIN_DAYS_PER_BUCKET, PATTERN_DAYS, SLEEP_THRESHOLD, WEEK_DAYS } from "./constants.js"` (don't import the module twice).

- [ ] **Step 4: Run to verify it passes** — `npx vitest run src/domain/insights.pattern.test.js` → PASS.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): brightest/hardest day + sleep-mood pattern`

---

### Task A6: `computeInsights` — compose + gating (`insights.js` part 3)

Produces the exact 16-field object in Reference A.

**Files:**
- Modify: `Kenaz.Web/src/domain/insights.js` (append `computeInsights`)
- Test: `Kenaz.Web/src/domain/insights.compute.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/insights.compute.test.js`:
```js
import { expect, test } from "vitest"
import { addDaysIso } from "./dates.js"
import { computeInsights } from "./insights.js"

const NOW = new Date(2026, 4, 22, 9, 0, 0)
const TODAY = "2026-05-22"
const back = (n) => addDaysIso(TODAY, -n)
const rec = (date, fields = {}) => ({
	date, mood: null, energy: null, sleep: null, note: null,
	createdAt: "2026-05-22T09:00:00Z", updatedAt: "2026-05-22T09:00:00Z", ...fields,
})

test("empty store: no week data, null averages, streak 0, no highlights/teaser", () => {
	const i = computeInsights([], NOW)
	expect(i).toMatchObject({
		hasWeekData: false, moodAverage: null, energyAverage: null, sleepAverage: null,
		streakDays: 0, hasHighlights: false, brightestDay: null, hardestDay: null,
		sleepThreshold: 7, sleepPatternConfident: false, showSleepTeaser: false, teaserDirection: "None",
	})
})
test("week averages over the 7-day window", () => {
	const i = computeInsights([rec(TODAY, { mood: 8, energy: 6, sleep: 7 }), rec(back(2), { mood: 4, energy: 4, sleep: 5 })], NOW)
	expect(i).toMatchObject({ hasWeekData: true, moodAverage: 6, energyAverage: 5, sleepAverage: 6 })
})
test("highlights need two DISTINCT mood days", () => {
	expect(computeInsights([rec(TODAY, { mood: 9 }), rec(back(1), { mood: 3 })], NOW)).toMatchObject({
		hasHighlights: true, brightestDay: { date: TODAY }, hardestDay: { date: back(1) },
	})
	expect(computeInsights([rec(TODAY, { mood: 6 }), rec(back(1), { mood: 6 })], NOW)).toMatchObject({
		hasHighlights: false, brightestDay: null, hardestDay: null,
	})
	expect(computeInsights([rec(TODAY, { mood: 7 })], NOW)).toMatchObject({ hasHighlights: false })
})
test("brightestDay carries no note field", () => {
	const i = computeInsights([rec(TODAY, { mood: 9, note: "great" }), rec(back(1), { mood: 3 })], NOW)
	expect(i.brightestDay).toEqual({ date: TODAY, mood: 9, energy: null, sleep: null })
	expect("note" in i.brightestDay).toBe(false)
})
test("teaser: confident + gap >= 1 → MoreSleepBetter", () => {
	const c = []
	for (let i = 0; i < 5; i++) c.push(rec(back(i), { mood: 8, sleep: 8 }))
	for (let i = 5; i < 10; i++) c.push(rec(back(i), { mood: 7, sleep: 6 }))
	expect(computeInsights(c, NOW)).toMatchObject({ sleepPatternConfident: true, showSleepTeaser: true, teaserDirection: "MoreSleepBetter" })
})
test("teaser: gap below 1 → hidden/None", () => {
	const c = []
	for (let i = 0; i < 5; i++) c.push(rec(back(i), { mood: 8, sleep: 8 }))
	c.push(rec(back(5), { mood: 8, sleep: 6 }))
	for (let i = 6; i < 10; i++) c.push(rec(back(i), { mood: 7, sleep: 6 })) // short avg 7.2, gap 0.8
	expect(computeInsights(c, NOW)).toMatchObject({ showSleepTeaser: false, teaserDirection: "None" })
})
test("teaser: negative gap → LessSleepBetter", () => {
	const c = []
	for (let i = 0; i < 5; i++) c.push(rec(back(i), { mood: 7, sleep: 8 }))
	for (let i = 5; i < 10; i++) c.push(rec(back(i), { mood: 8, sleep: 6 }))
	expect(computeInsights(c, NOW)).toMatchObject({ showSleepTeaser: true, teaserDirection: "LessSleepBetter" })
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/insights.compute.test.js` → FAIL.

- [ ] **Step 3: Implement** — append to `src/domain/insights.js`:
```js
import { TEASER_GAP } from "./constants.js" // fold into the combined constants import at the top

function toHighlight(c) {
	return c ? { date: c.date, mood: c.mood, energy: c.energy, sleep: c.sleep } : null
}

/** Build the 16-field Insights object the View consumes (Reference A). */
export function computeInsights(checkIns, now = new Date()) {
	const brightest = bestDay(checkIns, now)
	const hardest = worstDay(checkIns, now)
	const hasHighlights = !!brightest && !!hardest && brightest.date !== hardest.date

	const p = sleepMoodPattern(checkIns, now)
	let showSleepTeaser = false
	let teaserDirection = "None"
	if (p.isConfident && p.longSleepMoodAverage != null && p.shortSleepMoodAverage != null) {
		const gap = p.longSleepMoodAverage - p.shortSleepMoodAverage
		if (gap >= TEASER_GAP) {
			showSleepTeaser = true
			teaserDirection = "MoreSleepBetter"
		} else if (gap <= -TEASER_GAP) {
			showSleepTeaser = true
			teaserDirection = "LessSleepBetter"
		}
	}

	return {
		moodAverage: averageField(checkIns, now, "mood"),
		energyAverage: averageField(checkIns, now, "energy"),
		sleepAverage: averageField(checkIns, now, "sleep"),
		streakDays: streakDays(checkIns, now),
		hasWeekData: last7Days(checkIns, now).length > 0,
		brightestDay: hasHighlights ? toHighlight(brightest) : null,
		hardestDay: hasHighlights ? toHighlight(hardest) : null,
		hasHighlights,
		sleepThreshold: p.threshold,
		longSleepDays: p.longSleepDays,
		shortSleepDays: p.shortSleepDays,
		longSleepMoodAverage: p.longSleepMoodAverage,
		shortSleepMoodAverage: p.shortSleepMoodAverage,
		sleepPatternConfident: p.isConfident,
		showSleepTeaser,
		teaserDirection,
	}
}
```
> The top of `insights.js` should now have ONE import line: `import { MIN_DAYS_PER_BUCKET, PATTERN_DAYS, SLEEP_THRESHOLD, TEASER_GAP, WEEK_DAYS } from "./constants.js"`.

- [ ] **Step 4: Run all tests** — `npm test` → Expected: every domain test passes.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): computeInsights compose + gating (16-field contract)`

---

### Task A7: Newer-wins merge (`merge.js`)

**Files:**
- Create: `Kenaz.Web/src/domain/merge.js`
- Test: `Kenaz.Web/src/domain/merge.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/merge.test.js`:
```js
import { expect, test } from "vitest"
import { merge } from "./merge.js"

const rec = (date, mood, updatedAt) => ({
	date, mood, energy: null, sleep: null, note: null,
	createdAt: "2026-05-22T09:00:00+00:00", updatedAt,
})

test("adds check-ins for new dates", () => {
	const r = merge([], [rec("2026-05-20", 5, "2026-05-22T09:00:00+00:00")])
	expect(r.added).toBe(1)
	expect(r.records.find((c) => c.date === "2026-05-20")).toBeTruthy()
})
test("updates when incoming is strictly newer", () => {
	const existing = [rec("2026-05-22", 3, "2026-05-22T09:00:00+00:00")]
	const incoming = [rec("2026-05-22", 9, "2026-05-22T10:00:00+00:00")]
	const r = merge(existing, incoming)
	expect(r.updated).toBe(1)
	expect(r.records.find((c) => c.date === "2026-05-22").mood).toBe(9)
})
test("keeps existing when incoming is older", () => {
	const existing = [rec("2026-05-22", 3, "2026-05-22T09:00:00+00:00")]
	const incoming = [rec("2026-05-22", 9, "2026-05-22T08:00:00+00:00")]
	const r = merge(existing, incoming)
	expect(r.unchanged).toBe(1)
	expect(r.records.find((c) => c.date === "2026-05-22").mood).toBe(3)
})
test("equal timestamps keep existing (strictly-newer wins)", () => {
	const existing = [rec("2026-05-22", 3, "2026-05-22T09:00:00+00:00")]
	const incoming = [rec("2026-05-22", 9, "2026-05-22T09:00:00+00:00")]
	const r = merge(existing, incoming)
	expect(r.unchanged).toBe(1)
	expect(r.records.find((c) => c.date === "2026-05-22").mood).toBe(3)
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/merge.test.js` → FAIL.

- [ ] **Step 3: Implement** — `src/domain/merge.js`:
```js
/** Reconcile incoming check-ins into existing by date. Strictly-newer UpdatedAt wins; ties keep existing.
 *  Compares timestamps as instants (parsed to epoch ms), not as strings. */
export function merge(existing, incoming) {
	const byDate = new Map(existing.map((c) => [c.date, c]))
	let added = 0
	let updated = 0
	let unchanged = 0
	for (const candidate of incoming) {
		const current = byDate.get(candidate.date)
		if (!current) {
			byDate.set(candidate.date, candidate)
			added++
		} else if (Date.parse(candidate.updatedAt) > Date.parse(current.updatedAt)) {
			byDate.set(candidate.date, candidate)
			updated++
		} else {
			unchanged++
		}
	}
	return { records: [...byDate.values()], added, updated, unchanged }
}
```

- [ ] **Step 4: Run to verify it passes** — `npx vitest run src/domain/merge.test.js` → PASS.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): newer-wins merge`

---

### Task A8: Export envelope + import parse (`archive.js`)

**Files:**
- Create: `Kenaz.Web/src/domain/archive.js`
- Test: `Kenaz.Web/src/domain/archive.test.js`

- [ ] **Step 1: Write the failing tests** — `src/domain/archive.test.js`:
```js
import { expect, test } from "vitest"
import { ImportError, parseImport, toExportDocument } from "./archive.js"

const rec = (date, fields = {}) => ({
	date, mood: null, energy: null, sleep: null, note: null,
	createdAt: "2026-05-22T08:00:00+00:00", updatedAt: "2026-05-22T20:00:00+00:00", ...fields,
})

test("export writes a PascalCase, schemaVersion-1 envelope", () => {
	const doc = toExportDocument([rec("2026-05-22", { mood: 7, sleep: 7.5, note: "ok" })], new Date(Date.UTC(2026, 4, 24)))
	expect(doc.SchemaVersion).toBe(1)
	expect(doc.CheckIns[0]).toMatchObject({ Date: "2026-05-22", Mood: 7, Sleep: 7.5, Note: "ok" })
	expect(doc.CheckIns[0].CreatedAt).toBe("2026-05-22T08:00:00+00:00")
})
test("import reads a PascalCase C# export (round-trip)", () => {
	const text = JSON.stringify(toExportDocument([rec("2026-05-22", { mood: 7 })]))
	const { records, skipped } = parseImport(text)
	expect(skipped).toBe(0)
	expect(records).toHaveLength(1)
	expect(records[0]).toMatchObject({ date: "2026-05-22", mood: 7 })
})
test("import tolerates camelCase too", () => {
	const text = JSON.stringify({ schemaVersion: 1, checkIns: [{ date: "2026-05-22", mood: 5 }] })
	const { records } = parseImport(text)
	expect(records[0]).toMatchObject({ date: "2026-05-22", mood: 5 })
})
test("import drops invalid records and counts them", () => {
	const text = JSON.stringify({
		SchemaVersion: 1,
		CheckIns: [
			{ Date: "2026-05-22", Mood: 7 },
			{ Date: "2026-05-21", Mood: 99 }, // out of range → skipped
		],
	})
	const { records, skipped } = parseImport(text)
	expect(records).toHaveLength(1)
	expect(skipped).toBe(1)
})
test("import silently de-dupes by date (first wins, not counted as skipped)", () => {
	const text = JSON.stringify({
		SchemaVersion: 1,
		CheckIns: [
			{ Date: "2026-05-22", Mood: 7 },
			{ Date: "2026-05-22", Mood: 2 },
		],
	})
	const { records, skipped } = parseImport(text)
	expect(records).toHaveLength(1)
	expect(records[0].mood).toBe(7)
	expect(skipped).toBe(0)
})
test("import rejects a newer schema version", () => {
	const text = JSON.stringify({ SchemaVersion: 2, CheckIns: [] })
	expect(() => parseImport(text)).toThrow(ImportError)
})
test("import rejects unreadable JSON", () => {
	expect(() => parseImport("not json")).toThrow(/readable Kenaz export/)
})
test("import does not let __proto__ pollute records", () => {
	const text = '{"SchemaVersion":1,"CheckIns":[{"Date":"2026-05-22","Mood":5,"__proto__":{"polluted":true}}]}'
	const { records } = parseImport(text)
	expect(records[0].polluted).toBeUndefined()
	expect({}.polluted).toBeUndefined()
})
```

- [ ] **Step 2: Run to verify it fails** — `npx vitest run src/domain/archive.test.js` → FAIL.

- [ ] **Step 3: Implement** — `src/domain/archive.js`:
```js
import { validateCheckIn } from "./checkin.js"

export const SCHEMA_VERSION = 1

export class ImportError extends Error {
	constructor(message) {
		super(message)
		this.name = "ImportError"
	}
}

const pick = (obj, pascal, camel) => (obj[pascal] !== undefined ? obj[pascal] : obj[camel])
const orNull = (v) => (v === undefined ? null : v)

/** Build the PascalCase export envelope (matches the C# JsonCheckInArchive). */
export function toExportDocument(records, exportedAt = new Date()) {
	return {
		SchemaVersion: SCHEMA_VERSION,
		ExportedAt: exportedAt.toISOString(),
		CheckIns: records.map((r) => ({
			Date: r.date,
			Mood: orNull(r.mood),
			Energy: orNull(r.energy),
			Sleep: orNull(r.sleep),
			Note: orNull(r.note),
			CreatedAt: r.createdAt,
			UpdatedAt: r.updatedAt,
		})),
	}
}

/** Parse an export file into { records, skipped }. Accepts PascalCase or camelCase.
 *  Validates each record, de-dupes by date (first wins, silent), rejects a newer schema. */
export function parseImport(text) {
	let doc
	try {
		doc = JSON.parse(text)
	} catch {
		throw new ImportError("That file isn't a readable Kenaz export.")
	}
	if (!doc || typeof doc !== "object") throw new ImportError("That file isn't a readable Kenaz export.")

	const version = pick(doc, "SchemaVersion", "schemaVersion") ?? 1
	if (version > SCHEMA_VERSION) throw new ImportError("That export was made by a newer version of Kenaz.")

	const list = pick(doc, "CheckIns", "checkIns") ?? []
	if (!Array.isArray(list)) throw new ImportError("That file isn't a readable Kenaz export.")

	const records = []
	const seen = new Set()
	let skipped = 0
	const nowIso = new Date().toISOString()

	for (const raw of list) {
		if (!raw || typeof raw !== "object") {
			skipped++
			continue
		}
		const date = pick(raw, "Date", "date")
		if (typeof date !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(date)) {
			skipped++
			continue
		}
		if (seen.has(date)) continue // duplicate date: silent skip (matches C#)

		// Build the record field-by-field — never spread untrusted input (blocks __proto__ pollution).
		const record = {
			date,
			mood: orNull(pick(raw, "Mood", "mood")),
			energy: orNull(pick(raw, "Energy", "energy")),
			sleep: orNull(pick(raw, "Sleep", "sleep")),
			note: orNull(pick(raw, "Note", "note")),
			createdAt: pick(raw, "CreatedAt", "createdAt") ?? nowIso,
			updatedAt: pick(raw, "UpdatedAt", "updatedAt") ?? nowIso,
		}
		if (validateCheckIn(record)) {
			skipped++
			continue
		}
		records.push(record)
		seen.add(date)
	}
	return { records, skipped }
}
```

- [ ] **Step 4: Run to verify it passes** — `npx vitest run src/domain/archive.test.js` → PASS. Then `npm test` → all green.

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): export envelope + import parser (PascalCase, validated)`

---

## Phase B — IndexedDB store

### Task B1: `store.js` (verify-by-running)

**Learning-mode first:** walk through IndexedDB — `open(name, version)`, the one-time `onupgradeneeded` where you create the object store, transactions, and why every call is async.

**Files:**
- Create: `Kenaz.Web/src/store.js`

- [ ] **Step 1: Implement** — `src/store.js`:
```js
/* ======================================================================
   src/store.js — LOCAL STORE (replaces the old api.js transport seam)
   The only module that touches IndexedDB. Model/View/Controller go
   through this. Returns/accepts the camelCase CheckIn shape (Reference A).
   ====================================================================== */

const DB_NAME = "kenaz"
const DB_VERSION = 1
const STORE = "checkins"

let dbPromise = null

function openDb() {
	if (dbPromise) return dbPromise
	dbPromise = new Promise((resolve, reject) => {
		const req = indexedDB.open(DB_NAME, DB_VERSION)
		req.onupgradeneeded = () => {
			const db = req.result
			if (!db.objectStoreNames.contains(STORE)) {
				db.createObjectStore(STORE, { keyPath: "date" }) // one row per local date
			}
		}
		req.onsuccess = () => resolve(req.result)
		req.onerror = () => reject(req.error)
	})
	return dbPromise
}

function reqToPromise(req) {
	return new Promise((resolve, reject) => {
		req.onsuccess = () => resolve(req.result)
		req.onerror = () => reject(req.error)
	})
}

async function objectStore(mode) {
	const db = await openDb()
	return db.transaction(STORE, mode).objectStore(STORE)
}

async function getCheckIns() {
	const all = await reqToPromise(await (await objectStore("readonly")).getAll())
	return all.sort((a, b) => b.date.localeCompare(a.date)) // newest first
}

async function putCheckIn(date, { mood, energy, sleep, note }, now = new Date()) {
	const store = await objectStore("readwrite")
	const existing = await reqToPromise(store.get(date))
	const nowIso = now.toISOString()
	const record = existing
		? { ...existing, mood: mood ?? null, energy: energy ?? null, sleep: sleep ?? null, note: note ?? null, updatedAt: nowIso }
		: { date, mood: mood ?? null, energy: energy ?? null, sleep: sleep ?? null, note: note ?? null, createdAt: nowIso, updatedAt: nowIso }
	await reqToPromise(store.put(record))
	return record
}

async function deleteCheckIn(date) {
	const store = await objectStore("readwrite")
	await reqToPromise(store.delete(date))
}

/** Bulk overwrite (used by import after merge). */
async function putMany(records) {
	const db = await openDb()
	const tx = db.transaction(STORE, "readwrite")
	const store = tx.objectStore(STORE)
	for (const r of records) store.put(r)
	await new Promise((resolve, reject) => {
		tx.oncomplete = () => resolve()
		tx.onerror = () => reject(tx.error)
		tx.onabort = () => reject(tx.error)
	})
}

export const store = { getCheckIns, putCheckIn, deleteCheckIn, putMany }
```

- [ ] **Step 2: Verify by running** — temporarily, from the browser devtools console on `npm run dev`, exercise it:
```js
// In DevTools console after `npm run dev`:
const { store } = await import("/src/store.js")
await store.putCheckIn("2026-06-24", { mood: 7, energy: 6, sleep: 7.5, note: "test" })
console.log(await store.getCheckIns()) // → [{date:"2026-06-24", mood:7, ...}]
await store.deleteCheckIn("2026-06-24")
console.log(await store.getCheckIns()) // → []
```
Expected: the record round-trips; Application → IndexedDB → `kenaz` → `checkins` shows the row. Delete empties it.

- [ ] **Step 3: Commit (GitHub Desktop)** — `feat(M7-pwa): IndexedDB store`

---

## Phase C — Wire controller to the local store (the daily loop goes standalone)

After this phase the app is fully usable on IndexedDB: check in, today glance, history edit/delete, weekly review — no API, no token.

### Task C1: Rewrite the controller; delete `api.js`

**Files:**
- Rewrite: `Kenaz.Web/src/controller/controller.js`
- Delete: `Kenaz.Web/src/api.js`

- [ ] **Step 1: Replace `controller.js` entirely** with:
```js
/* ======================================================================
   src/controller/controller.js — CONTROLLER (behavior)
   Handles actions → calls the local store + domain → updates the model.
   Never writes DOM. Standalone: no API, no token, no network.
   ====================================================================== */

import { validateCheckIn } from "../domain/checkin.js"
import { computeInsights } from "../domain/insights.js"
import { store } from "../store.js"
import { isoToday } from "../utils/format.js"

export function createController({ model, view }) {
	view.bindActions(handleAction)
	model.subscribe(() => view.render(model.getState()))

	// Phone/browser Back walks the tab history instead of leaving the app.
	window.addEventListener("popstate", (event) => {
		const tab = event.state?.tab
		if (tab) model.setActiveTab(tab)
	})

	async function init() {
		history.replaceState({ tab: model.getState().activeTab }, "")
		await refresh()
	}

	async function refresh() {
		try {
			const checkIns = await store.getCheckIns()
			const insights = computeInsights(checkIns, new Date())
			const today = checkIns.find((c) => c.date === isoToday()) ?? null
			model.setData({ checkIns, insights, today })
		} catch {
			model.setNotice("Couldn't read your check-ins. Tap Retry.")
		}
	}

	async function handleAction(action, detail) {
		switch (action) {
			case "select-tab":
				selectTab(detail.tab)
				break
			case "save-checkin":
				await saveCheckIn(detail)
				break
			case "edit-checkin":
				model.setEditingDate(detail.date)
				break
			case "cancel-edit":
				model.setEditingDate(null)
				break
			case "ask-delete":
				model.setConfirmingDelete(detail.date)
				break
			case "cancel-delete":
				model.setConfirmingDelete(null)
				break
			case "delete-checkin":
				await deleteCheckIn(detail.date)
				break
			case "retry":
				await refresh()
				break
			case "export-data":
				await exportData()
				break
			case "import-data":
				await importData(detail.file)
				break
		}
	}

	function selectTab(tab) {
		if (tab === model.getState().activeTab) return
		history.pushState({ tab }, "")
		model.setActiveTab(tab)
	}

	async function saveCheckIn(detail) {
		const error = validateCheckIn(detail)
		if (error) {
			model.setFormError(error)
			return
		}
		try {
			await store.putCheckIn(detail.date, {
				mood: detail.mood,
				energy: detail.energy,
				sleep: detail.sleep,
				note: detail.note,
			})
			model.setEditingDate(null)
			await refresh()
			view.announce("Check-in saved.")
		} catch {
			model.setNotice("Couldn't save your check-in. Try again.")
		}
	}

	async function deleteCheckIn(date) {
		try {
			await store.deleteCheckIn(date)
			model.setEditingDate(null)
			await refresh()
			view.announce("Check-in deleted.")
		} catch {
			model.setNotice("Couldn't delete that check-in. Try again.")
		}
	}

	// Export/import wired in Phase D; the handlers below are filled there.
	async function exportData() {}
	async function importData(_file) {}

	return { init }
}
```
> Note: `validate()` now lives in `domain/checkin.js` (Task A3) — the old inline copy is gone. The `save-token` case, the `!api.hasToken()` guard, `ApiError`/`routeError`, and the `unreachable`/`unauthorized`/offline branches are all removed: there is no server. `model.requireSetup`/`setConnection` are simply never called, so `model.js` and `view.js` need no change and `setup.js` is never rendered.

- [ ] **Step 2: Delete the obsolete transport seam** — remove `Kenaz.Web/src/api.js`.

- [ ] **Step 3: Verify by running** — `npm run dev`, open the app:
  - It loads straight to Today (no "Connect to Kenaz" token screen).
  - Check in (mood/energy/sleep/note) → saved; "Today" heading appears; the week glance shows once data exists.
  - History tab → the entry is listed; edit it (no duplicate); delete it (with confirm).
  - Add a few days (backdate by editing the form's date if exposed, or via the store helper) → Review tab shows averages/streak; highlights appear with ≥2 distinct moods.
  - Reload the page → data persists (IndexedDB).

- [ ] **Step 4: Commit (GitHub Desktop)** — `feat(M7-pwa): controller runs on IndexedDB + local insights; drop API/token`

---

## Phase D — Export/import Data screen (backup + migration path)

This is new UI (the web app never had it) and is required by the standalone design: it's how PC data reaches the phone and the only backup.

### Task D1: The Data screen + tab

**Files:**
- Create: `Kenaz.Web/src/view/screens/data.js`
- Modify: `Kenaz.Web/src/view/view.js` (add the tab + route)

- [ ] **Step 1: Create `src/view/screens/data.js`** (uses the same `el()` builder; user text never goes through innerHTML):
```js
import { el } from "../../utils/dom.js"

export function renderData(state) {
	// Plain visible status line — NOT a live region. Async results are announced through the
	// persistent #sr-status region via view.announce() (newly-inserted role="status" nodes
	// announce inconsistently across screen readers).
	const result = state.dataResult ? el("p", { class: "form-note" }, state.dataResult) : null

	const exportBtn = el(
		"button",
		{ type: "button", class: "btn", "data-action": "export-data" },
		"Export my check-ins",
	)

	// Keyboard-accessible import: a real <button> proxies clicks to a hidden file input.
	// The input is taken out of the tab order + a11y tree; the button carries the focus ring.
	const fileInput = el("input", {
		type: "file",
		id: "import-file",
		accept: "application/json,.json",
		class: "sr-only",
		"data-import-file": "true",
		tabindex: "-1",
		"aria-hidden": "true",
	})
	const importBtn = el(
		"button",
		{ type: "button", class: "btn", "data-trigger-import": "true" },
		"Import from a backup file",
	)

	return el(
		"section",
		{ class: "stack", "data-screen": "data", tabindex: "-1" },
		el("h2", {}, "Your data"),
		el(
			"p",
			{},
			"Everything stays on this device. Export saves a backup file; import merges one back in (newer entries win).",
		),
		el("div", { class: "stack stack-sm" }, exportBtn, importBtn, fileInput),
		el(
			"p",
			{ class: "form-note" },
			"Export regularly — a backup file is your only safety net if this device is lost or its storage is cleared. The file is unencrypted, so keep it somewhere private.",
		),
		result,
	)
}
```
> Class names (`stack`, `btn`, `form-note`, `sr-only`) come from the design system already used by the other screens — reuse them; do not invent new CSS. If `btn`/`form-note` differ in the design system, match whatever `form.js`/`today.js` use for buttons and helper text. The "export regularly / unencrypted" note satisfies master-spec Invariant #9 (export carries the unencrypted-file warning) and surfaces the N2 eviction backstop.

- [ ] **Step 2: Wire the tab + route in `view.js`** — three small edits:

  (a) Import the screen at the top, beside the other screen imports:
  ```js
  import { renderData } from "./screens/data.js"
  ```

  (b) In `screenFor(state)`, add a `data` case alongside the existing `history`/`review` switch:
  ```js
  if (state.activeTab === "data") return renderData(state)
  ```
  (Place it with the other `activeTab` checks; keep `renderToday` as the default.)

  (c) In the tab bar, add a fourth tab after `review`. Match the existing tab markup exactly — each tab is a button with `data-action="select-tab"` and `data-tab`. Example, mirroring the existing entries:
  ```js
  el("button", {
      class: "tab",
      "data-action": "select-tab",
      "data-tab": "data",
      "aria-current": state.activeTab === "data" ? "page" : null,
  }, "Data")
  ```
  Use the same element/classes the current tabs use (copy one and change `data-tab`/label to `data`/"Data").

  (d) Proxy the import button to the hidden file input — a pure View affordance (no controller action, so the DOM stays in the View). In `createView`'s existing **click** delegation, before the generic `[data-action]` handling, add:
  ```js
  const trigger = event.target.closest("[data-trigger-import]")
  if (trigger) {
      event.preventDefault()
      trigger.parentElement.querySelector("[data-import-file]")?.click()
      return
  }
  ```
  (e) When the hidden input receives a file, dispatch the import action. Extend the existing **change** handler so a `[data-import-file]` input fires `import-data` with the chosen file:
  ```js
  // inside the existing root.addEventListener("change", ...) handler:
  const fileEl = event.target.closest("[data-import-file]")
  if (fileEl?.files?.length) {
      onAction("import-data", { file: fileEl.files[0] })
      fileEl.value = "" // allow re-importing the same file
      return
  }
  ```
  (Keep the existing Skip-checkbox branch in that same handler. Keyboard path: Tab to the visible Import button → Enter → it clicks the hidden input → the OS file picker opens.)

- [ ] **Step 3: Add `dataResult` to the model** — in `src/model/model.js`, add `dataResult: null` to the initial `state`, and a setter (mirroring the existing `setNotice` style):
  ```js
  setDataResult(message) {
      state.dataResult = message
      notify()
  },
  ```
  Also clear it in `setActiveTab` alongside the other per-tab resets (`state.dataResult = null`).
  > This is the one model change; it follows the existing subscribe/notify pattern (no DOM, no IO).

- [ ] **Step 4: Verify by running** — `npm run dev` → a "Data" tab appears; the screen renders the two buttons. (Wiring the actions is the next task.)

- [ ] **Step 5: Commit (GitHub Desktop)** — `feat(M7-pwa): Data screen (export/import) + tab`

---

### Task D2: Implement export + import handlers

**Files:**
- Modify: `Kenaz.Web/src/controller/controller.js` (fill `exportData` / `importData`)

- [ ] **Step 1: Add the imports** at the top of `controller.js`:
```js
import { parseImport, toExportDocument } from "../domain/archive.js"
import { merge } from "../domain/merge.js"
```

- [ ] **Step 2: Implement the two handlers** (replace the empty stubs from C1):
```js
	async function exportData() {
		try {
			const checkIns = await store.getCheckIns()
			const doc = toExportDocument(checkIns, new Date())
			const blob = new Blob([JSON.stringify(doc, null, 2)], { type: "application/json" })
			const url = URL.createObjectURL(blob)
			const stamp = isoToday()
			const a = document.createElement("a")
			a.href = url
			a.download = `kenaz-backup-${stamp}.json`
			a.click()
			URL.revokeObjectURL(url)
			model.setDataResult(`Exported ${checkIns.length} check-in(s).`)
			view.announce("Check-ins exported.")
		} catch {
			model.setDataResult("Export failed. Try again.")
		}
	}

	async function importData(file) {
		if (!file) return
		try {
			const text = await file.text()
			const { records, skipped } = parseImport(text)
			const existing = await store.getCheckIns()
			const result = merge(existing, records)
			await store.putMany(result.records)
			await refresh()
			const parts = [`${result.added} added`, `${result.updated} updated`, `${result.unchanged} unchanged`]
			if (skipped > 0) parts.push(`${skipped} skipped`)
			model.setDataResult(`Import done — ${parts.join(", ")}.`)
			view.announce("Import complete.")
		} catch (err) {
			model.setDataResult(err?.message ?? "Import failed.")
		}
	}
```

- [ ] **Step 2: Verify by running** — `npm run dev`:
  - Add a couple of check-ins → Export → a `kenaz-backup-<date>.json` downloads; open it and confirm PascalCase keys + `SchemaVersion: 1`.
  - Delete one check-in, then Import the file you just saved → the deleted day returns; the result line reads e.g. "1 added, 0 updated, 1 unchanged".
  - Import a hand-edited file containing a `Mood: 99` row → it's skipped and counted.

- [ ] **Step 3: Commit (GitHub Desktop)** — `feat(M7-pwa): export downloads a backup; import merges one`

---

## Phase E — PWA (manifest, icons, service worker, build config)

### Task E1: Icons + manifest

**Files:**
- Create: `Kenaz.Web/public/icons/icon-192.png`, `icon-512.png`, `icon-maskable-512.png`
- Create: `Kenaz.Web/public/manifest.webmanifest`
- Modify: `Kenaz.Web/index.html`

- [ ] **Step 1: Create the icons** — export three PNGs into `public/icons/` from a simple Kenaz mark (the torch). Quickest path: take a 512×512 source (an SVG of the torch/flame on the app's dark background), export `icon-512.png` and a downscaled `icon-192.png`; for `icon-maskable-512.png`, add ~12% padding around the mark so Android's mask doesn't clip it. Any image tool or an online "PWA icon generator" is fine — this is a one-time manual asset step. Confirm the three files exist and open as valid PNGs.

- [ ] **Step 2: Create `public/manifest.webmanifest`**:
```json
{
	"name": "Kenaz",
	"short_name": "Kenaz",
	"description": "Your private daily wellbeing check-in.",
	"start_url": ".",
	"scope": ".",
	"display": "standalone",
	"background_color": "#0f0f12",
	"theme_color": "#0f0f12",
	"icons": [
		{ "src": "icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
		{ "src": "icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
		{ "src": "icons/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
	]
}
```
> Set `background_color`/`theme_color` to the app's actual dark surface. Read it from `Kenaz.Web/public/design-system/tokens/colors.css` (the page/app background token) and use that hex instead of the placeholder `#0f0f12` so the splash + status bar match the UI.

- [ ] **Step 3: Link it in `index.html`** — add to `<head>` (after the viewport meta):
```html
<link rel="manifest" href="manifest.webmanifest" />
<meta name="theme-color" content="#0f0f12" />
<link rel="apple-touch-icon" href="icons/icon-192.png" />
```
(Match `theme-color` to the manifest value. The apple-touch-icon costs nothing and helps if Kenaz is ever opened on iOS.)

- [ ] **Step 4: Commit (GitHub Desktop)** — `feat(M7-pwa): manifest + app icons`

---

### Task E2: Service worker + registration

**Learning-mode first:** walk through the SW lifecycle — `install` (precache the shell), `activate` (drop old caches), `fetch` (serve cache-first, fall back to network, fall back to the cached shell when offline), and why bumping `CACHE` forces an update.

**Files:**
- Create: `Kenaz.Web/public/sw.js`
- Create: `Kenaz.Web/src/pwa.js`
- Modify: `Kenaz.Web/src/app.js` (call the PWA setup)

- [ ] **Step 1: Create `public/sw.js`** (runtime caching — robust to Vite's hashed asset names, which are cached on first online load):
```js
const CACHE = "kenaz-v1"
const SHELL = ["./", "./index.html", "./manifest.webmanifest", "./icons/icon-192.png", "./icons/icon-512.png"]

self.addEventListener("install", (event) => {
	event.waitUntil(
		caches
			.open(CACHE)
			.then((cache) => cache.addAll(SHELL))
			.then(() => self.skipWaiting()),
	)
})

self.addEventListener("activate", (event) => {
	event.waitUntil(
		caches
			.keys()
			.then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
			.then(() => self.clients.claim()),
	)
})

self.addEventListener("fetch", (event) => {
	const { request } = event
	if (request.method !== "GET" || new URL(request.url).origin !== self.location.origin) return

	// Navigations: network-first so a new deploy is picked up while online;
	// fall back to the cached shell when offline.
	if (request.mode === "navigate") {
		event.respondWith(fetch(request).catch(() => caches.match("./index.html")))
		return
	}

	// Other same-origin GETs (hashed assets, icons): cache-first, but only cache
	// SUCCESSFUL responses — never cache a transient 404/500 (it would persist and brick the app).
	event.respondWith(
		caches.match(request).then(
			(cached) =>
				cached ||
				fetch(request).then((response) => {
					if (response.ok) {
						const copy = response.clone()
						caches.open(CACHE).then((cache) => cache.put(request, copy))
					}
					return response
				}),
		),
	)
})
```
> Navigations are network-first, so new deploys load automatically when online; the cached shell only serves when offline. Bump `CACHE` to `kenaz-v2` to purge stale cached assets (`activate` clears old caches).

- [ ] **Step 2: Create `src/pwa.js`**:
```js
/* SW registration + persistent-storage request. Scope follows Vite's base path. */
export function setupPwa() {
	if ("serviceWorker" in navigator) {
		window.addEventListener("load", () => {
			navigator.serviceWorker.register(`${import.meta.env.BASE_URL}sw.js`).catch(() => {})
		})
	}
	// Ask the browser to keep our IndexedDB from being evicted. Installed PWAs are usually
	// granted this; the Data screen's "export regularly" note is the backstop if not (N2).
	requestPersistence()
}

async function requestPersistence() {
	if (!navigator.storage?.persist) return
	try {
		if (!(await navigator.storage.persisted())) {
			await navigator.storage.persist()
		}
	} catch {
		/* best-effort; never block startup */
	}
}
```

- [ ] **Step 3: Call it from `app.js`** — add the import and call inside `createApp()` (one import + one line; the MVC wiring is unchanged):
```js
import { setupPwa } from "./pwa.js"
// ... inside createApp(), after controller.init():
setupPwa()
```

- [ ] **Step 4: Commit (GitHub Desktop)** — `feat(M7-pwa): service worker + registration + persistent storage`

---

### Task E3: Vite config for GitHub Pages

**Learning-mode first:** explain `base` — on Pages the app lives at `https://malinfossum.github.io/kenaz/`, so every asset URL must be prefixed with `/kenaz/`. `import.meta.env.BASE_URL` becomes `/kenaz/`, which is why the SW registers at `${BASE_URL}sw.js` and the manifest uses relative paths.

**Files:**
- Modify: `Kenaz.Web/vite.config.js`

- [ ] **Step 1: Replace `vite.config.js`** with:
```js
import { defineConfig } from "vite"

export default defineConfig({
	// Served from https://malinfossum.github.io/kenaz/ — every asset is under /kenaz/.
	base: "/kenaz/",
	build: {
		outDir: "dist",
		emptyOutDir: true,
	},
})
```
> The old `outDir: "../Kenaz.Api/wwwroot"` and the dev `proxy` are removed — there's no API to serve from or proxy to. `public/` (manifest, sw.js, icons, design-system) is copied into `dist/` automatically.

- [ ] **Step 2: Verify by running the production build locally**:
  - `npm run build` → outputs to `dist/`.
  - `npm run preview` → open the previewed URL. In DevTools → Application: a service worker is registered and the manifest is detected with no errors; Lighthouse → "Installable" passes. (Preview serves at `/kenaz/` so the base path is exercised.)
  - Toggle DevTools "Offline" and reload → the app still loads (shell from cache) and shows your data (IndexedDB).

- [ ] **Step 3: Commit (GitHub Desktop)** — `build(M7-pwa): Vite base path for GitHub Pages; drop wwwroot + proxy`

---

## Phase F — Deploy to GitHub Pages

### Task F1: GitHub Actions → Pages

**Files:**
- Create: `.github/workflows/deploy.yml`

- [ ] **Step 1: Create `.github/workflows/deploy.yml`** (repo root, not under `Kenaz.Web/`):
```yaml
name: Deploy PWA to GitHub Pages

on:
  push:
    branches: [main]
    paths: ["Kenaz.Web/**", ".github/workflows/deploy.yml"]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: true

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: Kenaz.Web
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
          cache-dependency-path: Kenaz.Web/package-lock.json
      - run: npm ci
      - run: npm test
      - run: npm run build
      - uses: actions/upload-pages-artifact@v3
        with:
          path: Kenaz.Web/dist

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - id: deployment
        uses: actions/deploy-pages@v4
```
> `npm ci` requires `package-lock.json` to be committed (Task A1). The workflow also runs `npm test` so a broken domain blocks deploy.

- [ ] **Step 2: Enable Pages (one-time, in the GitHub web UI)** — repo **Settings → Pages → Build and deployment → Source: GitHub Actions**. (No `gh-pages` branch; the Actions artifact is the source.)

- [ ] **Step 3: Commit + push (GitHub Desktop)** — `ci(M7-pwa): build + deploy PWA to GitHub Pages`. Pushing to `main` triggers the workflow; watch it go green in the Actions tab, then confirm `https://malinfossum.github.io/kenaz/` loads.

- [ ] **Step 4: Install on the Nothing Phone** — open `https://malinfossum.github.io/kenaz/` in Chrome → menu → **Install app** (or the install prompt) → it lands on the home screen and launches standalone (no browser chrome). Verify: check in; turn the PC off; airplane-mode the phone → the app still opens and works.

---

## Phase G — Migrate real data + release v1.0

### Task G1: Bring existing check-ins onto the phone

> **De-risk early (do this right after Tasks A8 + B1 exist, not at the end):** the PascalCase interchange is inferred from System.Text.Json defaults, not yet verified against a real file. In `npm run dev`, import an actual `checkins.backup-*.json` (or a fresh Console export) from your PC and confirm it parses with **0 skipped** and the right count. The importer tolerates both casings, so this should pass — running it early surfaces any surprise before the whole app depends on it.

- [ ] **Step 1: Export from the PC** — run the existing C# app and export your check-ins to a JSON backup (the Console export, or your preserved `%APPDATA%\Kenaz\checkins.backup-*.json`). This file is the PascalCase envelope the PWA importer reads.

- [ ] **Step 2: Get the file to the phone** — transfer it (e.g. via your file-sync of choice or a USB copy) so it's accessible from the phone's file picker.

- [ ] **Step 3: Import on the phone** — open the installed app → **Data** tab → **Import from a backup file** → pick the file. Confirm the result line counts your days as "added", and the Review/History show your real history.

- [ ] **Step 4: Spot-check** — compare a few days (and the streak/averages) against the PC app to confirm parity.

### Task G2: Tag v1.0 + release notes

- [ ] **Step 1: Write release notes** — a short `docs/releases/v1.0.md` (or a GitHub Release body) describing: Kenaz is now a standalone, installable Android PWA; data is local (IndexedDB) and never leaves the device; export/import is the backup path; the C# desktop track continues separately.

- [ ] **Step 2: Tag `v1.0`** — via GitHub Desktop (History → right-click the release commit → **Create Tag** `v1.0`), then push the tag. Optionally publish a GitHub Release from the tag with the notes above.

- [ ] **Step 3: Commit (GitHub Desktop)** — `docs(M7-pwa): v1.0 release notes` (the tag itself carries no file change).

---

## Self-review (performed against the spec)

**Spec coverage:**
- LAN-bind config → **intentionally dropped** (the pivot replaced it with standalone; no task, by design — matches the spec's Context section).
- IndexedDB store → Task B1. · Insights moved client-side → A4–A6. · Merge → A7. · Validation → A3. · Export/import (envelope + parser) → A8; UI → D1–D2. · PWA manifest/SW/install → E1–E2; build/base → E3; deploy → F1. · Migration → G1. · v1.0 tag → G2.
- Carried invariants: **#3** null-skipping (A4/A6 tests) · **#4** local-date upsert-by-date (B1 keyPath `date`, A2 local dates) · **#5** streak grace (A4) · **#6** import validation + `__proto__` guard + `el()` text-node escaping (A8 test, D1 note) · **#8** a11y preserved (View untouched; Data screen uses `el()` + design-system classes) · **#9** plaintext-at-rest (IndexedDB; export carries the same caveat — call it out in the v1.0 notes). New invariants **N1–N4** are structural and hold by construction.

**Placeholder scan:** the only deliberately-deferred bits are concrete asset/values, each with explicit instructions: the icon PNGs (E1, one-time manual export) and the exact theme/background hex (E1, read from the design-system token). No `TBD`/`TODO` logic remains; every code step has complete code.

**Type/name consistency:** the 16-field camelCase Insights object and the `{date,mood,energy,sleep,note,createdAt,updatedAt}` record are defined once (References A) and used identically across `insights.js`, `store.js`, `archive.js`, `merge.js`, and the controller. Store methods `getCheckIns`/`putCheckIn`/`deleteCheckIn`/`putMany` are named consistently in B1, C1, D2. Domain exports (`computeInsights`, `merge`, `parseImport`, `toExportDocument`, `validateCheckIn`, `addDaysIso`, `todayIso`, `withinWindow`) match their import sites.

**One scope flag for Malin:** Phase D adds export/import UI the M6.1 web app never had — it's required by the standalone backup/migration story (spec §5), but it's the one place the plan goes beyond "web parity" and lightly touches `view.js`/`model.js`. Everything else leaves the View untouched.
