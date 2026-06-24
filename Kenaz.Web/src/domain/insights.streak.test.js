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
test("streak ignores a future-dated check-in", () => {
	expect(streakDays([rec(TODAY), rec(addDaysIso(TODAY, 1))], NOW)).toBe(1)
})
