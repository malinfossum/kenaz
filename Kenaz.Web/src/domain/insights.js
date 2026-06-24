import { MIN_DAYS_PER_BUCKET, PATTERN_DAYS, SLEEP_THRESHOLD, TEASER_GAP, WEEK_DAYS } from "./constants.js"
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

/** Day with the extreme mood in the 7-day window. dir: "max" | "min". Tie → most recent date. */
function extremeDay(checkIns, now, dir) {
	const candidates = last7Days(checkIns, now).filter((c) => c.mood != null)
	if (candidates.length === 0) return null
	candidates.sort((a, b) => {
		if (a.mood !== b.mood) return dir === "max" ? b.mood - a.mood : a.mood - b.mood
		return b.date.localeCompare(a.date) // tie: most recent first
	})
	return candidates[0]
}

export function bestDay(checkIns, now) {
	return extremeDay(checkIns, now, "max")
}
export function worstDay(checkIns, now) {
	return extremeDay(checkIns, now, "min")
}

/** 30-day sleep→mood comparison, bucketed at SLEEP_THRESHOLD (inclusive long side). */
export function sleepMoodPattern(checkIns, now) {
	const qualified = windowed(checkIns, now, PATTERN_DAYS).filter((c) => c.mood != null && c.sleep != null)
	const long = qualified.filter((c) => c.sleep >= SLEEP_THRESHOLD)
	const short = qualified.filter((c) => c.sleep < SLEEP_THRESHOLD)
	return {
		threshold: SLEEP_THRESHOLD,
		longSleepDays: long.length,
		shortSleepDays: short.length,
		longSleepMoodAverage: average(long.map((c) => c.mood)),
		shortSleepMoodAverage: average(short.map((c) => c.mood)),
		isConfident: long.length >= MIN_DAYS_PER_BUCKET && short.length >= MIN_DAYS_PER_BUCKET,
	}
}

function toHighlight(c) {
	return c ? { date: c.date, mood: c.mood, energy: c.energy, sleep: c.sleep } : null
}

/** Build the 16-field Insights object the View consumes (Reference A). */
export function computeInsights(checkIns, now = new Date()) {
	const brightest = bestDay(checkIns, now)
	const hardest = worstDay(checkIns, now)
	const hasHighlights = !!brightest && !!hardest && brightest.date !== hardest.date

	const p = sleepMoodPattern(checkIns, now)
	let showSleepTeaser = false
	let teaserDirection = "None"
	if (p.isConfident && p.longSleepMoodAverage != null && p.shortSleepMoodAverage != null) {
		const gap = p.longSleepMoodAverage - p.shortSleepMoodAverage
		if (gap >= TEASER_GAP) {
			showSleepTeaser = true
			teaserDirection = "MoreSleepBetter"
		} else if (gap <= -TEASER_GAP) {
			showSleepTeaser = true
			teaserDirection = "LessSleepBetter"
		}
	}

	return {
		moodAverage: averageField(checkIns, now, "mood"),
		energyAverage: averageField(checkIns, now, "energy"),
		sleepAverage: averageField(checkIns, now, "sleep"),
		streakDays: streakDays(checkIns, now),
		hasWeekData: last7Days(checkIns, now).length > 0,
		brightestDay: hasHighlights ? toHighlight(brightest) : null,
		hardestDay: hasHighlights ? toHighlight(hardest) : null,
		hasHighlights,
		sleepThreshold: p.threshold,
		longSleepDays: p.longSleepDays,
		shortSleepDays: p.shortSleepDays,
		longSleepMoodAverage: p.longSleepMoodAverage,
		shortSleepMoodAverage: p.shortSleepMoodAverage,
		sleepPatternConfident: p.isConfident,
		showSleepTeaser,
		teaserDirection,
	}
}
