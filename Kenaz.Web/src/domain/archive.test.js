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
