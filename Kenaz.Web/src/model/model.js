/* ======================================================================
   src/model/model.js — MODEL (state only)
   NO DOM. NO fetch. NO localStorage. Subscribe/notify.
   ====================================================================== */

export function createModel() {
	const state = {
		activeTab: "today", // "today" | "history" | "review"
		needsSetup: false, // show the Setup screen (no token, or after a 401)
		setupError: null, // message under the token field
		checkIns: [], // CheckInResponse[] (newest first)
		today: null, // today's CheckInResponse, or null
		insights: null, // InsightsResponse, or null
		editingDate: null, // History: the date whose form is open inline, or null
		confirmingDelete: null, // History: the date awaiting delete-confirm, or null
		formError: null, // check-in form validation message
		notice: null, // app-level operational error (500 / unexpected) — shown as a top banner
		connection: "ok", // "ok" | "unreachable"
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
			notify()
		},

		requireSetup(error = null) {
			state.needsSetup = true
			state.setupError = error
			notify()
		},
		clearSetup() {
			state.needsSetup = false
			state.setupError = null
			notify()
		},

		setData({ checkIns, insights, today }) {
			state.checkIns = checkIns
			state.insights = insights
			state.today = today
			state.connection = "ok"
			state.notice = null
			notify()
		},
		setConnection(connection) {
			state.connection = connection
			notify()
		},

		setEditingDate(date) {
			state.editingDate = date
			state.confirmingDelete = null
			state.formError = null
			notify()
		},
		setConfirmingDelete(date) {
			state.confirmingDelete = date
			state.editingDate = null
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
	}
}
