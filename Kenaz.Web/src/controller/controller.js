/* ======================================================================
   src/controller/controller.js — CONTROLLER (behavior)
   Handles actions → calls api.js → updates the model. Never writes DOM.
   ====================================================================== */

import { api, ApiError } from "../api.js"
import { isoToday } from "../utils/format.js"

export function createController({ model, view }) {
	view.bindActions(handleAction)
	model.subscribe(() => view.render(model.getState()))

	// Phone/browser Back walks the tab history instead of leaving the app. A tab tap pushes a history
	// entry (selectTab); Back fires popstate and we restore that entry's tab — without re-pushing.
	window.addEventListener("popstate", (event) => {
		const tab = event.state?.tab
		if (tab) model.setActiveTab(tab)
	})

	async function init() {
		// Seed the opening screen so it's part of the history the Back button walks.
		history.replaceState({ tab: model.getState().activeTab }, "")
		if (!api.hasToken()) {
			model.requireSetup()
			return
		}
		await refresh()
	}

	async function refresh() {
		const [checkInsResult, insightsResult] = await Promise.allSettled([
			api.getCheckIns(),
			api.getInsights(),
		])

		// A 401 or unreachable from either call takes over the whole screen.
		const blocking = [checkInsResult, insightsResult].find(
			(r) =>
				r.status === "rejected" &&
				r.reason instanceof ApiError &&
				(r.reason.kind === "unauthorized" || r.reason.kind === "unreachable")
		)
		if (blocking) {
			routeError(blocking.reason)
			return
		}

		// Keep whatever loaded; a single failing endpoint shouldn't blank the rest.
		if (checkInsResult.status === "fulfilled") {
			const checkIns = checkInsResult.value
			const insights = insightsResult.status === "fulfilled" ? insightsResult.value : null
			const today = checkIns.find((c) => c.date === isoToday()) ?? null
			model.setData({ checkIns, insights, today })
		}

		// Surface a degraded endpoint (e.g. /insights faulted but /checkins is fine).
		if (checkInsResult.status === "rejected" || insightsResult.status === "rejected") {
			model.setNotice("Some of your data couldn't load. Tap Retry.")
		}
	}

	function routeError(err) {
		const kind = err instanceof ApiError ? err.kind : "server"
		if (kind === "unauthorized") {
			api.clearToken()
			model.requireSetup("That token didn't work. Paste it again.")
		} else if (kind === "unreachable") {
			model.setConnection("unreachable")
		} else if (kind === "validation") {
			// Shown inside the check-in form.
			model.setFormError(err.message)
		} else {
			// not-found / server / unexpected — an operational error, shown as a top banner
			// so it's visible on any screen, not only when a form is open.
			model.setNotice(err.message)
		}
	}

	async function handleAction(action, detail) {
		switch (action) {
			case "select-tab":
				selectTab(detail.tab)
				break
			case "save-token": {
				const token = (detail.token ?? "").trim()
				if (!token) {
					model.requireSetup("Paste the token to continue.")
					break
				}
				api.setToken(token)
				model.clearSetup()
				await refresh()
				break
			}
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
				model.setConnection("ok")
				await refresh()
				break
		}
	}

	// A tab tap records a history entry so Back returns to the previous tab, not out of the app.
	// (Restoring on Back goes through model.setActiveTab directly — see the popstate listener.)
	function selectTab(tab) {
		if (tab === model.getState().activeTab) return
		history.pushState({ tab }, "")
		model.setActiveTab(tab)
	}

	async function saveCheckIn(detail) {
		const error = validate(detail)
		if (error) {
			model.setFormError(error)
			return
		}
		try {
			await api.putCheckIn(detail.date, {
				mood: detail.mood,
				energy: detail.energy,
				sleep: detail.sleep,
				note: detail.note,
			})
			model.setEditingDate(null) // closes the History inline form if it was open
			await refresh()
			view.announce("Check-in saved.")
		} catch (err) {
			routeError(err)
		}
	}

	async function deleteCheckIn(date) {
		try {
			await api.deleteCheckIn(date)
			model.setEditingDate(null) // leave the edit form; the entry is gone
			await refresh()
			view.announce("Check-in deleted.")
		} catch (err) {
			routeError(err)
		}
	}

	return { init }
}

/** Mirrors CheckIn.Validate so the user gets instant feedback; the 400 is the backstop. */
function validate({ mood, energy, sleep, note }) {
	if (mood != null && (mood < 1 || mood > 10)) return "Mood must be between 1 and 10 when provided."
	if (energy != null && (energy < 1 || energy > 10))
		return "Energy must be between 1 and 10 when provided."
	if (sleep != null && (sleep < 0 || sleep > 24))
		return "Sleep must be between 0 and 24 hours when provided."
	if (mood == null && energy == null && sleep == null && !(note && note.trim()))
		return "A check-in needs at least one of: mood, energy, sleep, or a note."
	return null
}
