import { el } from "../../utils/dom.js"

export function renderReview(_state) {
	return el("div", { class: "stack" }, el("h1", {}, "Weekly review"))
}
