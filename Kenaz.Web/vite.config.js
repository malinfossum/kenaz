import { defineConfig } from "vite"

export default defineConfig({
	// Build straight into the API's wwwroot so the API serves the app same-origin.
	// outDir is outside the project root, so emptyOutDir must be explicit.
	build: {
		outDir: "../Kenaz.Api/wwwroot",
		emptyOutDir: true,
	},
	// Dev only: proxy the API routes to the loopback API. The client always uses
	// relative URLs, so the same code is same-origin in production and proxied here.
	server: {
		proxy: {
			"/checkins": "http://127.0.0.1:5247",
			"/insights": "http://127.0.0.1:5247",
		},
	},
})
