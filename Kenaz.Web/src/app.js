/* ======================================================================
   src/app.js — APP WIRING
   DO NOT TOUCH (usually)
   - This file connects Model, View, Controller.
   - You normally edit ONLY:
     - src/model/model.js
     - src/view/view.js
     - src/controller/controller.js
   ====================================================================== */

import { createModel } from "./model/model.js"
import { createView } from "./view/view.js"
import { createController } from "./controller/controller.js"

export function createApp() {
	// If you rename the root element in index.html, update it here once.
	const root = document.getElementById("main")
	if (!root) throw new Error("Missing #main element in index.html")

	const model = createModel()
	const view = createView(root)
	const controller = createController({ model, view })

	// SKELETON: no premade behavior.
	// When YOU implement controller.init(), you can enable this line:
	// controller.init?.();
}
