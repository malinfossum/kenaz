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
