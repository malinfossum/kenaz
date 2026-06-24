const CACHE = "kenaz-v1"
const SHELL = ["./", "./index.html", "./manifest.webmanifest", "./icons/icon-192.png", "./icons/icon-512.png"]

self.addEventListener("install", (event) => {
	event.waitUntil(
		caches
			.open(CACHE)
			.then((cache) => cache.addAll(SHELL))
			.then(() => self.skipWaiting()),
	)
})

self.addEventListener("activate", (event) => {
	event.waitUntil(
		caches
			.keys()
			.then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
			.then(() => self.clients.claim()),
	)
})

self.addEventListener("fetch", (event) => {
	const { request } = event
	if (request.method !== "GET" || new URL(request.url).origin !== self.location.origin) return

	// Navigations: network-first so a new deploy is picked up while online;
	// fall back to the cached shell when offline.
	if (request.mode === "navigate") {
		event.respondWith(fetch(request).catch(() => caches.match("./index.html")))
		return
	}

	// Other same-origin GETs (hashed assets, icons): cache-first, but only cache
	// SUCCESSFUL responses — never cache a transient 404/500 (it would persist and brick the app).
	event.respondWith(
		caches.match(request).then(
			(cached) =>
				cached ||
				fetch(request).then((response) => {
					if (response.ok) {
						const copy = response.clone()
						caches.open(CACHE).then((cache) => cache.put(request, copy))
					}
					return response
				}),
		),
	)
})
