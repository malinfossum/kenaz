import { WEEK_DAYS } from "./constants.js"
import { addDaysIso, todayIso, withinWindow } from "./dates.js"

/** Mean of an array of numbers; null on empty. Caller filters out null fields first. */
export function average(values) {
	if (values.length === 0) return null
	return values.reduce((a, b) => a + b, 0) / values.length
}

/** Check-ins within a `days`-long window ending today, newest first. */
export function windowed(checkIns, now, days) {
	const today = todayIso(now)
	return checkIns
		.filter((c) => withinWindow(c.date, today, days))
		.sort((a, b) => b.date.localeCompare(a.date))
}

export function last7Days(checkIns, now) {
	return windowed(checkIns, now, WEEK_DAYS)
}

/** 7-day mean of one field, nulls skipped, null when the window has no value. */
export function averageField(checkIns, now, field, days = WEEK_DAYS) {
	const values = windowed(checkIns, now, days)
		.map((c) => c[field])
		.filter((v) => v != null)
	return average(values)
}

/** Consecutive logged days; one one-day gap forgiven, two in a row break it. */
export function streakDays(checkIns, now) {
	const today = todayIso(now)
	const logged = new Set(checkIns.map((c) => c.date).filter((d) => d <= today))
	if (logged.size === 0) return 0

	let cursor = [...logged].sort().at(-1) // most recent logged day <= today
	let streak = 0
	while (true) {
		if (logged.has(cursor)) {
			streak++
			cursor = addDaysIso(cursor, -1)
		} else if (logged.has(addDaysIso(cursor, -1))) {
			cursor = addDaysIso(cursor, -1) // forgive a single gap (no increment)
		} else {
			break
		}
	}
	return streak
}
