/* ======================================================================
   src/view/view.js — VIEW (render only)
   Renders the active screen from state; forwards actions via data-action.
   NO state mutation, NO fetch. User text reaches the DOM only through el().
   ====================================================================== */

import { el, clear } from "../utils/dom.js"
import { renderToday } from "./screens/today.js"
import { renderHistory } from "./screens/history.js"
import { renderReview } from "./screens/review.js"
import { renderData } from "./screens/data.js"

const TABS = [
	{ id: "history", label: "History" },
	{ id: "today", label: "Today" },
	{ id: "review", label: "Review" },
	{ id: "data", label: "Data" },
]

export function createView(root) {
	let onAction = () => {}

	// Clicks on anything carrying data-action (tabs, row edit, delete, retry…).
	root.addEventListener("click", (event) => {
		const trigger = event.target.closest("[data-trigger-import]")
		if (trigger) {
			event.preventDefault()
			trigger.parentElement.querySelector("[data-import-file]")?.click()
			return
		}
		const target = event.target.closest("[data-action]")
		if (!target || !root.contains(target)) return
		if (target.tagName === "FORM") return // a submit button's closest [data-action] is its form — let submit handle it
		event.preventDefault()
		onAction(target.dataset.action, { ...target.dataset })
	})

	// Form submit (the check-in form).
	root.addEventListener("submit", (event) => {
		const form = event.target.closest("form[data-action]")
		if (!form) return
		event.preventDefault()
		if (form.dataset.action === "save-checkin") onAction("save-checkin", readCheckInForm(form))
	})

	// Live slider readouts (pure View affordance: reflect input value into its <output>).
	root.addEventListener("input", (event) => {
		const slider = event.target.closest("input[type=range][data-output]")
		if (!slider) return
		const out = root.querySelector(`#${slider.dataset.output}`)
		if (out) out.textContent = slider.value
	})

	// Import file picker, plus the Skip toggles (enable/disable the matching slider + blank its readout).
	root.addEventListener("change", (event) => {
		const fileEl = event.target.closest("[data-import-file]")
		if (fileEl?.files?.length) {
			onAction("import-data", { file: fileEl.files[0] })
			fileEl.value = "" // allow re-importing the same file
			return
		}
		const skip = event.target.closest("input[type=checkbox][data-skip]")
		if (!skip) return
		const slider = root.querySelector(`#${skip.dataset.skip}`)
		if (!slider) return
		slider.disabled = skip.checked
		const out = root.querySelector(`#${slider.dataset.output}`)
		if (out) out.textContent = skip.checked ? "—" : slider.value
	})

	let lastScreenKey = null

	function render(state) {
		clear(root)
		root.append(renderShell(state))
		manageFocus(state.activeTab)
		lastScreenKey = state.activeTab
	}

	// An element asking for focus (form error, delete-confirm Cancel) wins. Otherwise move focus to the
	// screen top when the screen changed, or when the previous render stranded focus on the body: clear()
	// detaches the focused node, so a same-screen re-render with no [data-autofocus] (the post-load data
	// refresh, or opening an inline edit) would otherwise leave focus nowhere. A re-render that keeps focus
	// on a live control (validation, delete-confirm) sets [data-autofocus] and is handled by the early
	// return above, so this never yanks focus mid-interaction.
	function manageFocus(screenKey) {
		const wanted = root.querySelector("[data-autofocus]")
		if (wanted) {
			wanted.focus({ preventScroll: true })
			return
		}
		const focusStranded = !document.activeElement || document.activeElement === document.body
		if (screenKey !== lastScreenKey || focusStranded) {
			const screen = root.querySelector("[data-screen]")
			if (screen) screen.focus({ preventScroll: true })
		}
	}

	// Visually-hidden live region (in index.html, outside #main) for async confirmations.
	function announce(message) {
		const region = document.getElementById("sr-status")
		if (region) region.textContent = message
	}

	return {
		bindActions(handler) {
			onAction = handler
		},
		render,
		announce,
	}
}

function renderShell(state) {
	const frag = document.createDocumentFragment()
	frag.append(el("header", { class: "app-header" }, el("span", { class: "brand" }, "Kenaz")))
	if (state.notice) frag.append(renderNotice(state.notice))

	const screen = el("div", { class: "screen", "data-screen": "", tabindex: "-1" })
	screen.append(screenFor(state))
	frag.append(screen)

	frag.append(renderTabbar(state.activeTab))
	return frag
}

function screenFor(state) {
	if (state.activeTab === "history") return renderHistory(state)
	if (state.activeTab === "review") return renderReview(state)
	if (state.activeTab === "data") return renderData(state)
	return renderToday(state)
}

function renderTabbar(active) {
	const nav = el("nav", { class: "tabbar", "aria-label": "Sections" })
	for (const tab of TABS) {
		const isActive = tab.id === active
		nav.append(
			el(
				"button",
				{
					class: "tab",
					type: "button",
					"data-action": "select-tab",
					dataset: { tab: tab.id },
					"aria-current": isActive ? "page" : null,
				},
				tab.label
			)
		)
	}
	return nav
}

function renderNotice(message) {
	return el(
		"div",
		{ class: "offline-banner alert alert-danger", role: "alert" },
		el("span", {}, message),
		el("button", { class: "btn btn-ghost", type: "button", "data-action": "retry" }, "Retry")
	)
}

/** Read the check-in form into the action detail the Controller expects. */
function readCheckInForm(form) {
	const data = new FormData(form)
	const num = (v) => (v === "" || v == null ? null : Number(v))
	// A metric is null when its Skip box is checked, else the slider's 1–10 value.
	const metric = (name) => (form.elements[`${name}-skip`]?.checked ? null : num(data.get(name)))
	const note = (data.get("note") ?? "").trim()
	return {
		date: form.dataset.date,
		mood: metric("mood"),
		energy: metric("energy"),
		sleep: num(data.get("sleep")), // optional → null when blank
		note: note || null,
	}
}
