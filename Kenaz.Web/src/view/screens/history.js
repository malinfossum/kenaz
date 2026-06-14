import { el } from "../../utils/dom.js"
import { formatDate, mes, snippet } from "../../utils/format.js"
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
		...state.checkIns.map((c) => renderRow(c, state.confirmingDelete === c.date))
	)
	return el("div", { class: "stack" }, el("h1", {}, "History"), list)
}

function renderRow(checkIn, confirming) {
	const note = snippet(checkIn.note, 40)
	const main = el(
		"button",
		{
			class: "history-main",
			type: "button",
			"data-action": "edit-checkin",
			dataset: { date: checkIn.date },
		},
		el("span", { class: "history-date" }, formatDate(checkIn.date)),
		note ? el("span", { class: "history-note text-faint" }, note) : null,
		el("span", { class: "history-values" }, mes(checkIn))
	)

	const controls = confirming
		? el(
				"span",
				{ class: "cluster-sm" },
				el(
					"button",
					{
						class: "btn btn-danger",
						type: "button",
						"data-action": "delete-checkin",
						dataset: { date: checkIn.date },
					},
					"Delete"
				),
				el(
					"button",
					{
						class: "btn btn-ghost",
						type: "button",
						"data-action": "cancel-delete",
						"data-autofocus": "",
					},
					"Cancel"
				)
			)
		: el(
				"button",
				{
					class: "icon-btn",
					type: "button",
					"data-action": "ask-delete",
					dataset: { date: checkIn.date },
					"aria-label": `Delete the check-in for ${formatDate(checkIn.date)}`,
				},
				"✕"
			)

	return el("li", { class: "history-row" }, main, controls)
}
