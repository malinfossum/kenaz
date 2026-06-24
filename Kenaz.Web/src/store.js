/* ======================================================================
   src/store.js — LOCAL STORE (replaces the old api.js transport seam)
   The only module that touches IndexedDB. Model/View/Controller go
   through this. Returns/accepts the camelCase CheckIn shape (Reference A).

   IndexedDB note: a transaction auto-commits once control returns to the
   event loop, so we never `await` between creating a transaction and
   issuing its requests. Each operation opens a transaction, issues every
   request synchronously (the read-modify-write issues its put inside the
   get's onsuccess — still the transaction's active window), and resolves
   on `tx.oncomplete`. This avoids TransactionInactiveError.
   ====================================================================== */

const DB_NAME = "kenaz"
const DB_VERSION = 1
const STORE = "checkins"

let dbPromise = null

function openDb() {
	if (dbPromise) return dbPromise
	dbPromise = new Promise((resolve, reject) => {
		const req = indexedDB.open(DB_NAME, DB_VERSION)
		req.onupgradeneeded = () => {
			const db = req.result
			if (!db.objectStoreNames.contains(STORE)) {
				db.createObjectStore(STORE, { keyPath: "date" }) // one row per local date
			}
		}
		req.onsuccess = () => resolve(req.result)
		req.onerror = () => reject(req.error)
	})
	return dbPromise
}

/** All check-ins, newest first. */
async function getCheckIns() {
	const db = await openDb()
	return new Promise((resolve, reject) => {
		const tx = db.transaction(STORE, "readonly")
		const req = tx.objectStore(STORE).getAll()
		tx.oncomplete = () => resolve(req.result.sort((a, b) => b.date.localeCompare(a.date)))
		tx.onerror = () => reject(tx.error)
		tx.onabort = () => reject(tx.error)
	})
}

/** Upsert one date: create (stamping createdAt+updatedAt) or edit (advancing updatedAt). */
async function putCheckIn(date, { mood, energy, sleep, note }, now = new Date()) {
	const db = await openDb()
	const nowIso = now.toISOString()
	return new Promise((resolve, reject) => {
		const tx = db.transaction(STORE, "readwrite")
		const store = tx.objectStore(STORE)
		const getReq = store.get(date)
		let record
		getReq.onsuccess = () => {
			const existing = getReq.result
			record = existing
				? {
						...existing,
						mood: mood ?? null,
						energy: energy ?? null,
						sleep: sleep ?? null,
						note: note ?? null,
						updatedAt: nowIso,
					}
				: {
						date,
						mood: mood ?? null,
						energy: energy ?? null,
						sleep: sleep ?? null,
						note: note ?? null,
						createdAt: nowIso,
						updatedAt: nowIso,
					}
			store.put(record) // issued inside get's onsuccess → still the transaction's active window
		}
		tx.oncomplete = () => resolve(record)
		tx.onerror = () => reject(tx.error)
		tx.onabort = () => reject(tx.error)
	})
}

/** Remove one date (no-op if absent). */
async function deleteCheckIn(date) {
	const db = await openDb()
	return new Promise((resolve, reject) => {
		const tx = db.transaction(STORE, "readwrite")
		tx.objectStore(STORE).delete(date)
		tx.oncomplete = () => resolve()
		tx.onerror = () => reject(tx.error)
		tx.onabort = () => reject(tx.error)
	})
}

/** Bulk write (used by import after merge). Atomic: one transaction. */
async function putMany(records) {
	const db = await openDb()
	return new Promise((resolve, reject) => {
		const tx = db.transaction(STORE, "readwrite")
		const store = tx.objectStore(STORE)
		for (const r of records) store.put(r)
		tx.oncomplete = () => resolve()
		tx.onerror = () => reject(tx.error)
		tx.onabort = () => reject(tx.error)
	})
}

export const store = { getCheckIns, putCheckIn, deleteCheckIn, putMany }
