/* ======================================================================
   src/model/model.js — MODEL (state only)
   NO DOM. NO fetch. NO localStorage. Subscribe/notify.
   ====================================================================== */

export function createModel() {
	const state = {
		activeTab: "today", // "today" | "history" | "review" | "data"
		checkIns: [], // CheckInResponse[] (newest first)
		today: null, // today's CheckInResponse, or null
		insights: null, // InsightsResponse, or null
		editingDate: null, // History: the date whose form is open inline, or null
		confirmingDelete: null, // History: the date awaiting delete-confirm (inside the edit form), or null
		formError: null, // check-in form validation message
		notice: null, // app-level operational error (e.g. storage read/write failure) — shown as a top banner
		dataResult: null, // Data screen: last export/import status message, or null
	}

	const subscribers = []
	const notify = () => {
		for (const fn of subscribers) fn()
	}

	return {
		getState: () => state,
		subscribe(fn) {
			subscribers.push(fn)
		},

		setActiveTab(tab) {
			state.activeTab = tab
			state.editingDate = null
			state.confirmingDelete = null
			state.formError = null
			state.notice = null
			state.dataResult = null
			notify()
		},

		setData({ checkIns, insights, today }) {
			state.checkIns = checkIns
			state.insights = insights
			state.today = today
			state.notice = null
			notify()
		},

		setEditingDate(date) {
			state.editingDate = date
			state.confirmingDelete = null
			state.formError = null
			notify()
		},
		setConfirmingDelete(date) {
			// Keeps editingDate set — the confirm lives inside the open edit form.
			state.confirmingDelete = date
			notify()
		},
		setFormError(message) {
			state.formError = message
			notify()
		},
		setNotice(message) {
			state.notice = message
			notify()
		},
		setDataResult(message) {
			state.dataResult = message
			notify()
		},
	}
}
