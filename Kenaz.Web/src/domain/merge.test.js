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
