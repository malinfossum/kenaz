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
