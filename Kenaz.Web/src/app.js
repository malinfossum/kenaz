/* ======================================================================
   src/app.js — APP WIRING
   Connects Model, View, Controller, then starts the Controller.
   ====================================================================== */

import { createModel } from "./model/model.js"
import { createView } from "./view/view.js"
import { createController } from "./controller/controller.js"

export function createApp() {
	const root = document.getElementById("main")
	if (!root) throw new Error("Missing #main element in index.html")

	const model = createModel()
	const view = createView(root)
	const controller = createController({ model, view })

	controller.init()
}
