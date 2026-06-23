import { el } from "../../utils/dom.js"
import { formatDate, avg, hours, metrics } from "../../utils/format.js"

export function renderReview(state) {
	const ins = state.insights

	if (!ins || !ins.hasWeekData) {
		return el(
			"div",
			{ class: "stack" },
			el("h1", {}, "Weekly review"),
			el(
				"div",
				{ class: "empty-state" },
				el(
					"p",
					{},
					"Not enough check-ins yet — your weekly review will appear here once you've logged a few days."
				)
			)
		)
	}

	const wrap = el("div", { class: "stack" }, el("h1", {}, "Weekly review"), renderSummary(ins))
	if (ins.hasHighlights) wrap.append(renderHighlights(ins))
	wrap.append(renderPattern(ins))
	return wrap
}

function renderSummary(ins) {
	return el(
		"div",
		{ class: "card stack stack-sm" },
		el("h2", {}, "Your week"),
		el(
			"div",
			{ class: "stat-row" },
			stat("Mood", avg(ins.moodAverage)),
			stat("Energy", avg(ins.energyAverage)),
			stat("Sleep", ins.sleepAverage == null ? "—" : hours(ins.sleepAverage)),
			stat("Streak", `${ins.streakDays} ${ins.streakDays === 1 ? "day" : "days"}`)
		)
	)
}

function renderHighlights(ins) {
	return el(
		"div",
		{ class: "grid grid-2" },
		dayCard("Brightest day", ins.brightestDay),
		dayCard("Hardest day", ins.hardestDay)
	)
}

function dayCard(label, day) {
	return el(
		"div",
		{ class: "card stack stack-sm" },
		el("span", { class: "stat-label" }, label),
		el("span", { class: "history-date" }, formatDate(day.date)),
		el("span", { class: "history-values" }, metrics(day))
	)
}

function renderPattern(ins) {
	const card = el("div", { class: "card stack stack-sm" }, el("h2", {}, "Sleep & mood"))
	if (ins.sleepPatternConfident) {
		const threshold = Number(ins.sleepThreshold).toFixed(1).replace(/\.0$/, "")
		card.append(
			el(
				"p",
				{ class: "text-muted" },
				`On nights you slept ≥${threshold} h, your mood averaged ${avg(ins.longSleepMoodAverage)} — vs ${avg(ins.shortSleepMoodAverage)} on shorter nights.`
			),
			el(
				"p",
				{ class: "text-faint" },
				`(last 30 days: ${ins.longSleepDays} longer-sleep days, ${ins.shortSleepDays} shorter)`
			)
		)
	} else {
		card.append(
			el(
				"p",
				{ class: "text-muted" },
				"Not enough sleep + mood data yet — keep logging both and the pattern will show up here."
			)
		)
	}
	return card
}

function stat(label, value) {
	return el(
		"div",
		{ class: "stat" },
		el("span", { class: "stat-value" }, value),
		el("span", { class: "stat-label" }, label)
	)
}
