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
