import { el } from "../../utils/dom.js"

export function renderSetup(state) {
	return el(
		"div",
		{ class: "setup" },
		el(
			"div",
			{ class: "card stack" },
			el("h1", {}, "Connect to Kenaz"),
			el(
				"p",
				{ class: "text-muted" },
				"Paste the token Kenaz printed when it started. You can also find it in %APPDATA%\\Kenaz\\api-token."
			),
			state.setupError ? el("p", { class: "form-error", role: "alert" }, state.setupError) : null,
			el(
				"form",
				{ class: "stack", "data-action": "save-token" },
				el(
					"div",
					{ class: "field" },
					el("label", { class: "label", for: "token" }, "API token"),
					el("input", {
						class: "input",
						id: "token",
						name: "token",
						type: "password",
						autocomplete: "off",
						autocapitalize: "off",
						"data-autofocus": "",
					})
				),
				el("button", { class: "btn btn-primary btn-full", type: "submit" }, "Connect")
			)
		)
	)
}
