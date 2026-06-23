import { el } from "../../utils/dom.js"

/**
 * The reusable check-in form. Sliders always provide mood + energy (1–10);
 * sleep and note are optional. User text (the note) is set as a text child,
 * so it is escaped by construction.
 *
 * @param {object} opts
 * @param {string} opts.date           yyyy-MM-dd this form writes to
 * @param {object|null} opts.checkIn    existing CheckInResponse to pre-fill, or null
 * @param {string|null} opts.error      validation message to show, or null
 * @param {boolean} [opts.showCancel]   show a Cancel button (History-edit)
 * @param {boolean} [opts.showDelete]   show a Delete button (History-edit)
 * @param {boolean} [opts.confirmingDelete]  swap the buttons for a delete confirmation
 */
export function renderCheckInForm({
	date,
	checkIn,
	error,
	showCancel = false,
	showDelete = false,
	confirmingDelete = false,
}) {
	// Existing check-in: mood/energy may be null (skipped). New check-in: default 5, not skipped.
	const moodValue = checkIn ? checkIn.mood : 5
	const energyValue = checkIn ? checkIn.energy : 5

	return el(
		"form",
		{ class: "checkin-form stack", "data-action": "save-checkin", dataset: { date } },
		metricField("Mood", "mood", moodValue),
		metricField("Energy", "energy", energyValue),
		el(
			"div",
			{ class: "field" },
			el("label", { class: "label", for: "sleep" }, "Sleep (hours)"),
			el("input", {
				class: "input",
				id: "sleep",
				name: "sleep",
				type: "number",
				min: "0",
				max: "24",
				step: "0.5",
				inputmode: "decimal",
				value: checkIn?.sleep ?? "",
			})
		),
		el(
			"div",
			{ class: "field" },
			el("label", { class: "label", for: "note" }, "Note"),
			el(
				"textarea",
				{ class: "textarea", id: "note", name: "note", rows: "3" },
				checkIn?.note ?? ""
			)
		),
		!confirmingDelete && error
			? el("p", { class: "form-error", role: "alert", tabindex: "-1", "data-autofocus": "" }, error)
			: null,
		confirmingDelete
			? el(
					"div",
					{ class: "stack stack-sm" },
					el(
						"p",
						{ role: "alert", tabindex: "-1", "data-autofocus": "" },
						"Delete this check-in? This can't be undone."
					),
					el(
						"div",
						{ class: "cluster" },
						el(
							"button",
							{ class: "btn btn-danger", type: "button", "data-action": "delete-checkin", dataset: { date } },
							"Delete"
						),
						el("button", { class: "btn btn-ghost", type: "button", "data-action": "cancel-delete" }, "Cancel")
					)
				)
			: el(
					"div",
					{ class: "cluster" },
					el("button", { class: "btn btn-primary", type: "submit" }, checkIn ? "Update" : "Save"),
					showCancel
						? el("button", { class: "btn btn-ghost", type: "button", "data-action": "cancel-edit" }, "Cancel")
						: null,
					showDelete
						? el(
								"button",
								{ class: "btn btn-danger", type: "button", "data-action": "ask-delete", dataset: { date } },
								"Delete"
							)
						: null
				)
	)
}

// A 1–10 slider with a "Skip" toggle. `value` is a number, or null when the metric was skipped.
// Skipping disables the slider; readCheckInForm then sends null for this metric.
function metricField(label, name, value) {
	const skipped = value == null
	const sliderValue = value ?? 5
	const outId = `${name}-out`
	return el(
		"div",
		{ class: "field" },
		el(
			"div",
			{ class: "cluster-between metric-head" },
			el(
				"span",
				{ class: "metric-label" },
				el("label", { class: "label", for: name }, label),
				el(
					"output",
					{ class: "slider-value", id: outId, for: name },
					skipped ? "—" : String(sliderValue)
				)
			),
			el(
				"label",
				{ class: "skip-toggle" },
				el("input", {
					type: "checkbox",
					name: `${name}-skip`,
					dataset: { skip: name },
					checked: skipped,
					"aria-label": `Skip ${label.toLowerCase()}`,
				}),
				el("span", {}, "Skip")
			)
		),
		el("input", {
			class: "slider",
			type: "range",
			id: name,
			name,
			min: "1",
			max: "10",
			step: "1",
			value: String(sliderValue),
			disabled: skipped,
			dataset: { output: outId },
		})
	)
}
