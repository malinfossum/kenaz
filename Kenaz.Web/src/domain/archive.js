import { validateCheckIn } from "./checkin.js"

export const SCHEMA_VERSION = 1

export class ImportError extends Error {
	constructor(message) {
		super(message)
		this.name = "ImportError"
	}
}

const has = (o, k) => Object.prototype.hasOwnProperty.call(o, k)
const pick = (obj, pascal, camel) => (has(obj, pascal) ? obj[pascal] : has(obj, camel) ? obj[camel] : undefined)
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
