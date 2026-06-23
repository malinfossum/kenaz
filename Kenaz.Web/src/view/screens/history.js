import { el } from "../../utils/dom.js"
import { formatDate, metrics, snippet } from "../../utils/format.js"
import { renderCheckInForm } from "./form.js"

export function renderHistory(state) {
	// Inline edit takes over the screen when a row is open.
	if (state.editingDate) {
		const checkIn = state.checkIns.find((c) => c.date === state.editingDate) ?? null
		return el(
			"div",
			{ class: "stack" },
			el("h1", {}, `Edit ${formatDate(state.editingDate)}`),
			renderCheckInForm({
				date: state.editingDate,
				checkIn,
				error: state.formError,
				showCancel: true,
				showDelete: true,
				confirmingDelete: state.confirmingDelete === state.editingDate,
			})
		)
	}

	if (state.checkIns.length === 0) {
		return el(
			"div",
			{ class: "stack" },
			el("h1", {}, "History"),
			el(
				"div",
				{ class: "empty-state" },
				el("p", {}, "No check-ins yet. Head to Today to add your first.")
			)
		)
	}

	const list = el(
		"ul",
		{ class: "history-list" },
		...state.checkIns.map((c) => renderRow(c))
	)
	return el("div", { class: "stack" }, el("h1", {}, "History"), list)
}

function renderRow(checkIn) {
	const date = checkIn.date
	const note = snippet(checkIn.note, 40)
	return el(
		"li",
		{ class: "history-row" },
		el(
			"div",
			{ class: "history-main" },
			el("span", { class: "history-date" }, formatDate(date)),
			note ? el("span", { class: "history-note text-faint" }, note) : null,
			el("span", { class: "history-values" }, metrics(checkIn))
		),
		el(
			"button",
			{
				class: "icon-btn",
				type: "button",
				"data-action": "edit-checkin",
				dataset: { date },
				"aria-label": `Edit the check-in for ${formatDate(date)}`,
			},
			"✎"
		)
	)
}
