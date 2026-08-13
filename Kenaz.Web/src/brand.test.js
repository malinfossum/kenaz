import { existsSync, readFileSync } from "node:fs"
import { dirname, join } from "node:path"
import { fileURLToPath } from "node:url"
import { describe, expect, test } from "vitest"

const here = dirname(fileURLToPath(import.meta.url))
export const web = join(here, "..")
export const repo = join(web, "..")

const read = (...parts) => readFileSync(join(...parts), "utf8")

describe("palette wiring", () => {
	test("the shell opts into the Kenaz palette and the Fraunces type skin", () => {
		const html = read(web, "index.html")
		expect(html).toContain('data-palette="kenaz"')
		expect(html).toContain('data-typeskin="fraunces"')
	})

	test("the extracted design-system actually defines that palette", () => {
		const css = read(web, "public/design-system/tokens/palettes/kenaz.css")
		expect(css).toContain("--accent-rgb: 217 154 78")
		expect(css).toContain("--border: #322b22")
	})

	test("main.css carries no colour overrides — colour belongs to the palette", () => {
		const css = read(web, "src/styles/main.css")
		expect(css).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
	})

	test("the dead Setup rules are gone", () => {
		const css = read(web, "src/styles/main.css")
		expect(css).not.toContain(".setup")
	})

	test("the wordmark uses the display font", () => {
		const css = read(web, "src/styles/main.css")
		expect(css).toMatch(/\.brand\s*\{[^}]*var\(--font-display\)/)
	})

	test("the service worker cache was bumped", () => {
		expect(existsSync(join(web, "public/sw.js"))).toBe(true)
		expect(read(web, "public/sw.js")).toContain('const CACHE = "kenaz-v2"')
	})
})

describe("svg sources", () => {
	test("all three sources exist", () => {
		for (const f of ["mark.svg", "mark-small.svg", "banner.svg"]) {
			expect(existsSync(join(repo, "docs/brand", f))).toBe(true)
		}
	})

	test("the master mark carries the leaf veins", () => {
		expect(read(repo, "docs/brand/mark.svg")).toContain("#f6e7cb")
	})

	test("the small mark drops the veins and thickens the strokes", () => {
		const svg = read(repo, "docs/brand/mark-small.svg")
		expect(svg).not.toContain("#f6e7cb")
		expect(svg).toContain('stroke-width="4"')
	})

	test("the banner asks for Fraunces", () => {
		expect(read(repo, "docs/brand/banner.svg")).toContain("Fraunces")
	})
})
