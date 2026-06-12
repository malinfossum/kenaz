import { el } from "../../utils/dom.js"

export function renderToday(_state) {
	return el("div", { class: "stack" }, el("h1", {}, "Today"))
}
