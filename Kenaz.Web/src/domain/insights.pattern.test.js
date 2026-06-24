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
