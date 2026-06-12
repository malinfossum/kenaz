import { el } from "../../utils/dom.js"

export function renderHistory(_state) {
	return el("div", { class: "stack" }, el("h1", {}, "History"))
}
