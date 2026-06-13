import { el } from "../../utils/dom.js"
import { isoToday, avg, hours } from "../../utils/format.js"
import { renderCheckInForm } from "./form.js"

export function renderToday(state) {
	const today = isoToday()
	const wrap = el(
		"div",
		{ class: "stack" },
		el("h1", {}, state.today ? "Today" : "Check in"),
		renderCheckInForm({ date: today, checkIn: state.today, error: state.formError })
	)
	if (state.insights?.hasWeekData) wrap.append(renderGlance(state.insights))
	return wrap
}

function renderGlance(insights) {
	const card = el(
		"div",
		{ class: "card stack-sm" },
		el("h2", {}, "Last 7 days"),
		el(
			"div",
			{ class: "stat-row" },
			stat("Mood", avg(insights.moodAverage)),
			stat("Energy", avg(insights.energyAverage)),
			stat("Sleep", insights.sleepAverage == null ? "—" : hours(insights.sleepAverage)),
			stat("Streak", `${insights.streakDays} ${insights.streakDays === 1 ? "day" : "days"}`)
		)
	)
	if (insights.showSleepTeaser && insights.teaserDirection !== "None") {
		card.append(
			el(
				"p",
				{ class: "text-muted" },
				insights.teaserDirection === "MoreSleepBetter"
					? "A small pattern: you've felt better on nights with more sleep. See Review."
					: "A small pattern: you've felt better on shorter-sleep nights. See Review."
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
