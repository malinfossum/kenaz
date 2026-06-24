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
