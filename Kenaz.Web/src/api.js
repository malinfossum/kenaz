/* ======================================================================
   src/api.js — TRANSPORT SEAM
   The ONLY module that calls fetch or touches the auth token.
   Model/View/Controller go through this; they never fetch directly.
   ====================================================================== */

const TOKEN_KEY = "kenaz.token"

/** A classified failure. `kind` is one of:
 *  "unauthorized" | "validation" | "not-found" | "server" | "unreachable". */
export class ApiError extends Error {
	constructor(kind, message) {
		super(message)
		this.name = "ApiError"
		this.kind = kind
	}
}

function currentToken() {
	return localStorage.getItem(TOKEN_KEY) ?? ""
}

async function request(method, path, body) {
	const headers = { Authorization: `Bearer ${currentToken()}` }
	if (body !== undefined) headers["Content-Type"] = "application/json"

	let response
	try {
		response = await fetch(path, {
			method,
			headers,
			body: body === undefined ? undefined : JSON.stringify(body),
		})
	} catch {
		// Network failure / API not running.
		throw new ApiError("unreachable", "Can't reach Kenaz.")
	}

	if (response.status === 401) throw new ApiError("unauthorized", "Token rejected.")
	if (response.status === 400) {
		// The server's body is a JSON-quoted string; client validation already showed the specific
		// rule, so the backstop just needs a clean, generic message.
		throw new ApiError("validation", "That check-in didn't validate.")
	}
	if (response.status === 404) throw new ApiError("not-found", "No check-in for that day.")
	if (response.status >= 500) throw new ApiError("server", "Something went wrong on Kenaz's side.")

	if (response.status === 204) return null
	try {
		return await response.json()
	} catch {
		// A success status with a body we can't parse still breaks the contract that
		// request() only ever resolves or throws a classified ApiError — so reclassify.
		throw new ApiError("server", "Something went wrong on Kenaz's side.")
	}
}

export const api = {
	hasToken: () => Boolean(localStorage.getItem(TOKEN_KEY)),
	setToken: (token) => localStorage.setItem(TOKEN_KEY, token),
	clearToken: () => localStorage.removeItem(TOKEN_KEY),

	getCheckIns: () => request("GET", "/checkins"),
	getInsights: () => request("GET", "/insights"),
	putCheckIn: (date, data) => request("PUT", `/checkins/${date}`, data),
	deleteCheckIn: (date) => request("DELETE", `/checkins/${date}`),
}
