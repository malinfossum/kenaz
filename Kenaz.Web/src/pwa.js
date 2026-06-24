/* SW registration + persistent-storage request. Scope follows Vite's base path. */
export function setupPwa() {
	if ("serviceWorker" in navigator) {
		window.addEventListener("load", () => {
			navigator.serviceWorker.register(`${import.meta.env.BASE_URL}sw.js`).catch(() => {})
		})
	}
	// Ask the browser to keep our IndexedDB from being evicted. Installed PWAs are usually
	// granted this; the Data screen's "export regularly" note is the backstop if not (N2).
	requestPersistence()
}

async function requestPersistence() {
	if (!navigator.storage?.persist) return
	try {
		if (!(await navigator.storage.persisted())) {
			await navigator.storage.persist()
		}
	} catch {
		/* best-effort; never block startup */
	}
}
