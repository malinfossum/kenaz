import { el } from "../../utils/dom.js"

export function renderData(state) {
	// Plain visible status line — NOT a live region. Async results are announced through the
	// persistent #sr-status region via view.announce() (newly-inserted role="status" nodes
	// announce inconsistently across screen readers).
	const result = state.dataResult ? el("p", { class: "form-note" }, state.dataResult) : null

	const exportBtn = el(
		"button",
		{ type: "button", class: "btn", "data-action": "export-data" },
		"Export my check-ins",
	)

	// Keyboard-accessible import: a real <button> proxies clicks to a hidden file input.
	// The input is taken out of the tab order + a11y tree; the button carries the focus ring.
	const fileInput = el("input", {
		type: "file",
		id: "import-file",
		accept: "application/json,.json",
		class: "sr-only",
		"data-import-file": "true",
		tabindex: "-1",
		"aria-hidden": "true",
	})
	const importBtn = el(
		"button",
		{ type: "button", class: "btn", "data-trigger-import": "true" },
		"Import from a backup file",
	)

	return el(
		"section",
		{ class: "stack", "data-screen": "data", tabindex: "-1" },
		el("h2", {}, "Your data"),
		el(
			"p",
			{},
			"Everything stays on this device. Export saves a backup file; import merges one back in (newer entries win).",
		),
		el("div", { class: "stack stack-sm" }, exportBtn, importBtn, fileInput),
		el(
			"p",
			{ class: "form-note" },
			"Export regularly — a backup file is your only safety net if this device is lost or its storage is cleared. The file is unencrypted, so keep it somewhere private.",
		),
		result,
	)
}
