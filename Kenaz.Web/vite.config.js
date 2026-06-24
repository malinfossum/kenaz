import { defineConfig } from "vite"

export default defineConfig({
	// Served from https://malinfossum.github.io/kenaz/ — every asset lives under /kenaz/.
	base: "/kenaz/",
	build: {
		outDir: "dist",
		emptyOutDir: true,
	},
})
