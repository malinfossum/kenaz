/* ======================================================================
   src/utils/format.js — pure helpers (no DOM, no fetch)
   isoToday() is shared by the Controller and the screens.
   ====================================================================== */

/** Local "today" as yyyy-MM-dd (matches the API's date keys). */
export function isoToday() {
	const d = new Date()
	const pad = (n) => String(n).padStart(2, "0")
	return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** yyyy-MM-dd → "Wed 27 May" (display only; falls back to the raw string). */
export function formatDate(iso) {
	const parts = iso.split("-").map(Number)
	if (parts.length !== 3 || parts.some(Number.isNaN)) return iso
	const [y, m, day] = parts
	const d = new Date(y, m - 1, day)
	return d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" })
}

/** An average to one decimal, or an em dash when null/undefined. */
export function avg(value) {
	return value == null ? "—" : Number(value).toFixed(1)
}

/** A 1–10 scale value, or em dash. */
export function scale(value) {
	return value == null ? "—" : String(value)
}

/** Hours with a unit, or em dash. */
export function hours(value) {
	return value == null ? "—" : `${Number(value).toFixed(1).replace(/\.0$/, "")} h`
}

/** A readable "mood 7 · energy 6 · sleep 8 h" line for a check-in (em dash where skipped). */
export function metrics(checkIn) {
	return `mood ${scale(checkIn.mood)} · energy ${scale(checkIn.energy)} · sleep ${checkIn.sleep == null ? "—" : hours(checkIn.sleep)}`
}

/** First `max` chars of a note, trimmed, with an ellipsis; empty string when no note. */
export function snippet(note, max = 40) {
	if (!note) return ""
	const trimmed = note.trim()
	return trimmed.length > max ? `${trimmed.slice(0, max)}…` : trimmed
}
