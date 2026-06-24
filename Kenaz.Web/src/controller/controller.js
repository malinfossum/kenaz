/* ======================================================================
   src/controller/controller.js — CONTROLLER (behavior)
   Handles actions → calls the local store + domain → updates the model.
   Never writes DOM. Standalone: no API, no token, no network.
   ====================================================================== */

import { validateCheckIn } from "../domain/checkin.js"
import { computeInsights } from "../domain/insights.js"
import { store } from "../store.js"
import { isoToday } from "../utils/format.js"

export function createController({ model, view }) {
	view.bindActions(handleAction)
	model.subscribe(() => view.render(model.getState()))

	// Phone/browser Back walks the tab history instead of leaving the app.
	window.addEventListener("popstate", (event) => {
		const tab = event.state?.tab
		if (tab) model.setActiveTab(tab)
	})

	async function init() {
		history.replaceState({ tab: model.getState().activeTab }, "")
		await refresh()
	}

	async function refresh() {
		try {
			const checkIns = await store.getCheckIns()
			const insights = computeInsights(checkIns, new Date())
			const today = checkIns.find((c) => c.date === isoToday()) ?? null
			model.setData({ checkIns, insights, today })
		} catch {
			model.setNotice("Couldn't read your check-ins. Tap Retry.")
		}
	}

	async function handleAction(action, detail) {
		switch (action) {
			case "select-tab":
				selectTab(detail.tab)
				break
			case "save-checkin":
				await saveCheckIn(detail)
				break
			case "edit-checkin":
				model.setEditingDate(detail.date)
				break
			case "cancel-edit":
				model.setEditingDate(null)
				break
			case "ask-delete":
				model.setConfirmingDelete(detail.date)
				break
			case "cancel-delete":
				model.setConfirmingDelete(null)
				break
			case "delete-checkin":
				await deleteCheckIn(detail.date)
				break
			case "retry":
				await refresh()
				break
			case "export-data":
				await exportData()
				break
			case "import-data":
				await importData(detail.file)
				break
		}
	}

	function selectTab(tab) {
		if (tab === model.getState().activeTab) return
		history.pushState({ tab }, "")
		model.setActiveTab(tab)
	}

	async function saveCheckIn(detail) {
		const error = validateCheckIn(detail)
		if (error) {
			model.setFormError(error)
			return
		}
		try {
			await store.putCheckIn(detail.date, {
				mood: detail.mood,
				energy: detail.energy,
				sleep: detail.sleep,
				note: detail.note,
			})
			model.setEditingDate(null)
			await refresh()
			view.announce("Check-in saved.")
		} catch {
			model.setNotice("Couldn't save your check-in. Try again.")
		}
	}

	async function deleteCheckIn(date) {
		try {
			await store.deleteCheckIn(date)
			model.setEditingDate(null)
			await refresh()
			view.announce("Check-in deleted.")
		} catch {
			model.setNotice("Couldn't delete that check-in. Try again.")
		}
	}

	// Export/import wired in Phase D; the handlers below are filled there.
	async function exportData() {}
	async function importData(_file) {}

	return { init }
}
