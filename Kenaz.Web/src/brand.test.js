import { existsSync, readFileSync, statSync } from "node:fs"
import { dirname, join } from "node:path"
import { fileURLToPath } from "node:url"
import { describe, expect, test } from "vitest"

const here = dirname(fileURLToPath(import.meta.url))
export const web = join(here, "..")
export const repo = join(web, "..")

const read = (...parts) => readFileSync(join(...parts), "utf8")

describe("palette wiring", () => {
	test("the shell opts into the Kenaz palette", () => {
		expect(read(web, "index.html")).toContain('data-palette="kenaz"')
	})

	test("it declares no type skin — Sora/Figtree come from the design-system :root", () => {
		expect(read(web, "index.html")).not.toContain("data-typeskin")
	})

	test("the extracted design-system defines the Lantern palette, not the retired amber", () => {
		const css = read(web, "public/design-system/tokens/palettes/kenaz.css")
		expect(css).toContain("--accent-rgb: 124 154 179")
		expect(css).toContain("--border: #232c35")
		expect(css).not.toContain("217 154 78")
	})

	test("the palette is distinct from the design-system default", () => {
		const kenaz = read(web, "public/design-system/tokens/palettes/kenaz.css")
		const colors = read(web, "public/design-system/tokens/colors.css")
		// The 3.0.0 default is the warm amber-gold; if kenaz ever matches it again the
		// app is indistinguishable from an unbranded consumer.
		expect(colors).toContain("--accent-rgb: 222 166 72")
		expect(kenaz).not.toContain("--accent-rgb: 222 166 72")
	})

	test("main.css carries no colour overrides — colour belongs to the palette", () => {
		expect(read(web, "src/styles/main.css")).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
	})

	test("the dead Setup rules are gone", () => {
		expect(read(web, "src/styles/main.css")).not.toContain(".setup")
	})

	test("the wordmark uses the display font", () => {
		expect(read(web, "src/styles/main.css")).toMatch(/\.brand\s*\{[^}]*var\(--font-display\)/)
	})

	test("the service worker cache was bumped", () => {
		expect(existsSync(join(web, "public/sw.js"))).toBe(true)
		expect(read(web, "public/sw.js")).toContain('const CACHE = "kenaz-v3"')
	})
})

describe("svg sources", () => {
	test("the mark, the compact mark and the banner all exist", () => {
		for (const f of ["mark.svg", "mark-small.svg", "banner.svg"]) {
			expect(existsSync(join(repo, "docs/brand", f))).toBe(true)
		}
	})

	test("the marks are the Lantern, drawn in the palette's own colours", () => {
		for (const f of ["mark.svg", "mark-small.svg"]) {
			const svg = read(repo, "docs/brand", f)
			expect(svg).toContain("#7C9AB3") // --accent
			expect(svg).toContain("#90ADC5") // --accent-strong
			expect(svg).toContain("#F4F7FA") // --text
		}
	})

	test("the compact mark thickens its strokes for small sizes", () => {
		expect(read(repo, "docs/brand/mark-small.svg")).toContain('stroke-width="26"')
	})

	test("the logo lockups are kept alongside the marks", () => {
		for (const f of ["horizontal.svg", "stacked.svg", "wordmark.svg", "app-icon.svg"]) {
			expect(existsSync(join(repo, "docs/brand/logos", f))).toBe(true)
		}
	})
})

describe("header mark", () => {
	test("the header inlines a decorative mark", () => {
		const js = read(web, "src/view/view.js")
		expect(js).toContain("brand-mark")
		expect(js).toContain('"aria-hidden": "true"')
	})

	test("the inlined mark is the compact variant's geometry", () => {
		const js = read(web, "src/view/view.js")
		const frame = read(repo, "docs/brand/mark-small.svg").match(/d="(M256 84[^"]+)"/)[1]
		expect(js).toContain(frame)
	})

	test("the mark takes its colour from tokens, so it follows the light theme", () => {
		const js = read(web, "src/view/view.js")
		// A hex here would freeze the mark to dark mode; var() does not work in a
		// presentation attribute, so the fills must live in main.css.
		expect(
			js.slice(js.indexOf("function brandMark"), js.indexOf("function renderShell"))
		).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
		expect(read(web, "src/styles/main.css")).toMatch(
			/\.brand-mark \.mark-frame \{\s*stroke: var\(--accent\)/
		)
	})

	test("the mark is built from nodes, never innerHTML", () => {
		expect(read(web, "src/view/view.js")).not.toContain("innerHTML")
	})

	test("it does not re-announce the wordmark", () => {
		const js = read(web, "src/view/view.js")
		const at = js.indexOf('class: "app-header"')
		const header = js.slice(at, at + 300)
		expect(header).not.toContain("aria-label")
		expect(header).not.toContain('"role": "img"')
	})
})

describe("generated raster assets", () => {
	const sizes = {
		"public/icons/icon-32.png": 200,
		"public/icons/icon-192.png": 1000,
		"public/icons/icon-512.png": 3000,
		"public/icons/icon-maskable-192.png": 1000,
		"public/icons/icon-maskable-512.png": 3000,
		"public/icons/apple-touch-icon-180.png": 1000,
		"public/icons/favicon.ico": 1000,
		"public/icons/icon.svg": 200,
		"public/og.png": 20000,
	}

	for (const [rel, min] of Object.entries(sizes)) {
		test(`${rel} exists and is not a blank stub`, () => {
			const p = join(web, rel)
			expect(existsSync(p)).toBe(true)
			expect(statSync(p).size).toBeGreaterThan(min)
		})
	}

	test("the banner PNG is generated", () => {
		const p = join(repo, "docs/brand/banner.png")
		expect(existsSync(p)).toBe(true)
		expect(statSync(p).size).toBeGreaterThan(20000)
	})

	test("the retired generators are gone — the assets are vendored finals now", () => {
		expect(existsSync(join(web, "scripts/generate-icons.mjs"))).toBe(false)
		expect(existsSync(join(web, "scripts/generate-brand.ps1"))).toBe(false)
	})

	test("every icon the manifest names actually ships", () => {
		const manifest = JSON.parse(read(web, "public/manifest.webmanifest"))
		for (const icon of manifest.icons) {
			expect(existsSync(join(web, "public", icon.src))).toBe(true)
		}
		expect(manifest.icons.some((i) => i.purpose === "maskable")).toBe(true)
	})
})

describe("social metadata", () => {
	test("all five OG tags are present", () => {
		const html = read(web, "index.html")
		for (const p of ["og:title", "og:description", "og:image", "og:url", "og:type"]) {
			expect(html).toContain(`property="${p}"`)
		}
	})

	test("og:image and og:url are absolute — relative paths read as unreachable", () => {
		const html = read(web, "index.html")
		expect(html).toMatch(
			/property="og:image"\s+content="https:\/\/malinfossum\.github\.io\/kenaz\//
		)
		expect(html).toMatch(/property="og:url"\s+content="https:\/\/malinfossum\.github\.io\/kenaz\//)
	})

	test("a favicon is declared", () => {
		expect(read(web, "index.html")).toMatch(/rel="icon"/)
	})

	test("public copy claims device-locality, never encryption", () => {
		const html = read(web, "index.html").toLowerCase()
		expect(html).not.toContain("encrypted")
		expect(html).not.toContain("secure")
	})

	test("og:title and og:description sit in their target ranges", () => {
		const html = read(web, "index.html")
		const grab = (p) => html.match(new RegExp(`property="${p}"\\s+content="([^"]+)"`))[1]
		expect(grab("og:title").length).toBeGreaterThanOrEqual(50)
		expect(grab("og:title").length).toBeLessThanOrEqual(60)
		expect(grab("og:description").length).toBeGreaterThanOrEqual(110)
		expect(grab("og:description").length).toBeLessThanOrEqual(160)
	})
})

describe("readme", () => {
	test("it leads with the banner, carrying real alt text", () => {
		expect(read(repo, "README.md")).toContain(
			"![Kenaz — bring it into the light](docs/brand/banner.png)"
		)
	})

	test("the dead token-paste flow is gone", () => {
		const md = read(repo, "README.md")
		expect(md).not.toContain("M6.1")
		expect(md).not.toMatch(/paste the token/i)
	})

	test("it points at the live app", () => {
		expect(read(repo, "README.md")).toContain("https://malinfossum.github.io/kenaz/")
	})

	test("no email address leaks into a public artifact", () => {
		expect(read(repo, "README.md")).not.toMatch(/[\w.+-]+@[\w-]+\.[\w.]+/)
	})

	test("it claims device-locality, not encryption", () => {
		const md = read(repo, "README.md").toLowerCase()
		// "unencrypted" is the honest disclaimer on the export and must stay; what must
		// never appear is a claim that anything IS encrypted or secure.
		expect(md).not.toMatch(/(?<!un)encrypted/)
		expect(md).not.toMatch(/\bencryption\b/)
		expect(md).not.toMatch(/\bsecure\b/)
	})

	test("it does not promise a generator script that no longer exists", () => {
		expect(read(repo, "README.md")).not.toContain("generate-brand.ps1")
	})
})
