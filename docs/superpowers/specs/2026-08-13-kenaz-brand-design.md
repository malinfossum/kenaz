# Kenaz brand identity — design

Date: 2026-08-13
Status: approved, ready for planning

## Why

Kenaz shipped as v1.0 and is live at `https://malinfossum.github.io/kenaz/`, but it has no
identity. The icons are a placeholder accent disc, the app inherits the design-system's default
cool slate accent (`#7c9ab3`), and the README still describes a C# solution with a token-paste
web app that no longer exists. The name means *torch* and the whole story is fire and light,
yet nothing about the product looks like it.

This design gives Kenaz a mark, a palette, a typographic voice and a public face, and lands the
palette in the shared design-system rather than as a local fork.

## Goals

- A logo mark that works from 1280px down to a 32px favicon.
- A warm palette that matches the name, living in the canonical design-system as an opt-in brand palette.
- A banner and social card so the repo and the live URL present well when shared.
- A README that leads with the shipped product.

## Non-goals

- The native desktop app. That is a separate, later track; the C# projects stay untouched.
- Any change to app behaviour, features, storage or the domain logic.
- A light theme for the app. Kenaz is dark-only — `index.html` hardcodes `saved || "dark"` and
  nothing ever writes a `theme` key. The palette still ships light-mode channels for reuse.
- Editing design-system files inside the Kenaz repo. All design-system change happens in
  workbench and arrives by extract.

## Brand direction

Warm torchlight. Kenaz is the fire-family sibling to Ignite, but where Ignite is a bright spark
(`#ff7a18`, a literal flame, an energetic task app), Kenaz is steady evening light: deeper,
calmer, lower-energy. Same family, different temperature.

The calm comes from shape language, not just colour — rounded tips, symmetry, thin strokes, a
single contained light rather than anything flickering.

## The mark

A leaf-veined torch inside a ring, standing on a gentle horizon.

Three ideas carried by one shape:

- **Torch** — the name, literally.
- **Leaf** — a flame and a leaf share a silhouette, so the veins read as both a midrib and the
  flame's hot core. Nature is built into the object rather than added around it.
- **Horizon** — grounding and perspective, the one landscape cue that carries psychological
  meaning. Landscape scenery stays out of the mark and lives in the banner instead, where it
  cannot cost legibility.

The containing ring is what makes it calm: light held steady rather than radiating.

### Master source — `docs/brand/mark.svg`

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" role="img" aria-label="Kenaz">
  <circle cx="50" cy="50" r="44" fill="none" stroke="#d99a4e" stroke-width="1.7" opacity="0.32"/>
  <path d="M24 85 Q50 79 76 85" fill="none" stroke="#d99a4e" stroke-width="1.7" opacity="0.45" stroke-linecap="round"/>
  <path d="M46.5 18 Q50 12 53.5 18 C 62 32, 71 43, 71 53 C 71 64, 61.5 71, 50 71 C 38.5 71, 29 64, 29 53 C 29 43, 38 32, 46.5 18 Z" fill="#d99a4e"/>
  <g fill="none" stroke="#f6e7cb" stroke-linecap="round">
    <path d="M50 68 L50 17" stroke-width="2" opacity="0.9"/>
    <path d="M50 46 L41 37" stroke-width="1.5" opacity="0.6"/>
    <path d="M50 55 L59 46" stroke-width="1.5" opacity="0.6"/>
  </g>
  <rect x="32" y="74" width="36" height="7" rx="3.5" fill="#d99a4e"/>
  <rect x="44.5" y="83" width="11" height="9" rx="5" fill="#d99a4e"/>
</svg>
```

### Size variants

The hairline strokes are the constraint. At a 32px render, a `stroke-width` of 1.7 in a 100-unit
viewBox resolves to about 0.5px — the ring and horizon effectively disappear.

| Variant | File | Use | Changes from master |
|---|---|---|---|
| Master | `mark.svg` | 128px and above | — |
| Small | `mark-small.svg` | 64px and below (favicon) | Ring and horizon `stroke-width` 4; leaf veins removed; torch unchanged |
| Maskable | generated | Android maskable icon | Whole mark scaled to 62% of the tile, centred, on a full-bleed `#000000` ground |

The maskable variant is not optional. Android crops maskable icons to a circle at roughly 80% of
the tile, and the mark's ring sits at 88% — shipped as-is, the ring gets shaved off on the home
screen.

## Colour

| Token | Value | Role |
|---|---|---|
| Amber | `#d99a4e` | Accent — the torch, active states, stat values |
| Amber strong | `#e8b26a` | Hover, focus ring |
| Cream | `#f6e7cb` | The flame's core / leaf veins |
| Ink on amber | `#241704` | Text on solid amber fills |
| Page | `#000000` | Stays true black for OLED |
| Warm surfaces | `#0b0a09` → `#262016` | Raised surfaces, warmed from the default cool greys |

### Measured contrast

Every **text** pair clears WCAG AA (4.5:1).

| Pair | Ratio |
|---|---|
| Amber on black | 8.68:1 |
| Amber on `#0b0a09` | 8.18:1 |
| Amber on card `#14110d` | 7.78:1 |
| Text `#f4efe6` on black | 18.34:1 |
| Muted `#b8ada0` on black | 9.52:1 |
| Faint `#8f8578` on card | 5.19:1 |
| Ink `#241704` on amber | 7.24:1 |
| Light-mode accent `#9e641a` on `#f6f8fa` | 4.60:1 |
| White on light solid `#8a5716` | 6.08:1 |

Non-text contrast (WCAG 1.4.11, threshold 3:1) is a separate question:

| Pair | Ratio | Verdict |
|---|---|---|
| Focus outline `#e8b26a` on black | 11.01:1 | Passes comfortably |
| Focus outline on card `#14110d` | 9.87:1 | Passes comfortably |
| Border `#322b22` on black | 1.50:1 | **Fails** — see below |

The focus indicator is a solid `2px solid var(--accent-strong)` outline in `base.css`, not the
38%-alpha `--focus-ring` (that is only the box-shadow layer underneath it), so it passes with
room to spare.

Borders are the exception. `input.css` sets `border: 1px solid var(--border)`, which makes the
border a UI component boundary that 1.4.11 wants at 3:1. The design-system default `#232c35`
measures 1.48:1 and already fails; this is a design-system-wide issue, not one Kenaz introduces.

Kenaz's rule here is **do not regress it**: `--border` is `#322b22` (1.50:1), at parity with the
default rather than below it. An earlier draft used `#2a241c`, which measured 1.37:1 and would
have made Kenaz worse than every other app. The real fix — around `#6a5b45`, which reaches
3.20:1 — changes how Ignite, Wend and Tidsro look and belongs in a workbench issue, not in a
Kenaz branding slice.

## Typography

Fraunces, a warm editorial serif, for the wordmark and large headings; the default sans for
body. This needs no new font work — the design-system already self-hosts Fraunces as an OFL
woff2 in `assets/fonts/` and exposes it as the `fraunces` type skin, which drops h3/h4 back to
sans because serif reads poorly small.

Kenaz opts in rather than defining its own type tokens.

## Where the brand lives

In the canonical design-system at `workbench/libraries/design-system/`, as an opt-in brand
palette — the pattern already established by `ignite.css`, `wend.css` and `gold.css`.

Not a variant of `gold`: that is Tidsro's brighter yellow and it is accent-only, while Kenaz
needs warm surfaces as well as a warm accent.

### New file — `tokens/palettes/kenaz.css`

```css
/*
 * Kenaz — steady torchlight over a warm near-black ground.
 * Amber accent and warmed surfaces; the sibling to ignite.css, turned down.
 * Opt in with <html data-palette="kenaz">.
 */
[data-palette="kenaz"] {
	--surface-1: #000000;
	--surface-2: #0b0a09;
	--surface-3: #14110d;
	--surface-4: #1d1811;
	--surface-5: #262016;

	--text: #f4efe6;
	--text-muted: #b8ada0;
	--text-faint: #8f8578;

	/* #322b22 holds parity with the default --border (1.50 vs 1.48 on black).
	   Do not lower it — see the non-text contrast note above. */
	--border: #322b22;
	--border-strong: #3a3226;
	--border-soft: #1c1813;

	--accent-rgb: 217 154 78;
	--accent-strong-rgb: 232 178 106;
	--on-accent: #241704;
}

[data-theme="light"][data-palette="kenaz"] {
	--accent-rgb: 158 100 26;
	--accent-strong-rgb: 130 80 18;
	--accent-solid: #8a5716;
}
```

Registered with one `@import` line in `tokens/palettes/index.css`.

### The version gap

Kenaz.Web carries design-system **1.0.0**; canonical is **2.0.2**. The `data-palette` mechanism
does not exist before 2.0, so the palette approach requires upgrading Kenaz first, via
`node tools/extract.mjs design-system <target>`.

The base tokens are compatible — `--surface-*`, `--text*` and `--border*` are byte-identical
between the two versions, and 2.0's changes are otherwise additive: `-rgb` single-source
channels, `--accent-solid`, `--on-accent`, the palettes and type-skin layers, and new
`skeleton`, `tabs` and `toast` components.

Two things are **not** purely additive, and the visual pass should expect them:

- **Controls get 2px taller.** 2.0.2 is an accessibility patch that returns `.btn`,
  `.input`/`.textarea`/`.select`, `.tab` and `.icon-btn` to a `min-height` of `2.75rem` (44px)
  from the `2.625rem` (42px) that 2.0.0's shape pass introduced. This is an improvement Kenaz
  inherits for free — and it does not disturb Kenaz's own rules, which already sit at or above
  the floor (`.tab` is `3.25rem`, `.history-row .icon-btn` is `2.75rem`).
- **The shape pass itself.** 2.0.0 restyled components deliberately; going from 1.0.0 skips
  straight past it.

Kenaz is live and installed, so the upgrade needs a real visual pass across all four screens
before it ships rather than an assumption of safety.

## Application

### App

- `<html lang="en" data-palette="kenaz" data-typeskin="fraunces">` in `index.html`.
- `src/styles/main.css` already holds no colour overrides — it is layout-only on design-system
  tokens, so the palette lands without fighting anything. Two edits only:
  - `.brand` takes `var(--font-display)` and drops its hardcoded `font-weight: 700` and
    `letter-spacing`, both of which the type skin now owns.
  - The dead `.setup` and `.setup .card` rules come out. They are leftovers from the Setup
    screen removed in `bd8cd61`; no `setup` class exists anywhere in the view. (`.offline-banner`
    stays — it is still in use at `view.js:158`.)
- The header wordmark — `el("span", { class: "brand" }, "Kenaz")` in `view.js:116` — gains the
  mark alongside it. Two constraints on that inlined copy:
  - It uses **`mark-small.svg`**, not the master. At roughly 19px the master's 1.7-unit strokes
    resolve to about 0.3px and the ring and horizon disappear.
  - It drops `role="img"` and `aria-label`, and takes **`aria-hidden="true"`**. Those attributes
    are correct for the standalone file, but inlined beside the wordmark they make a screen
    reader announce "Kenaz Kenaz". The adjacent text is the accessible name.
- `index.html` gains `<link rel="icon">`, which it currently lacks.

Heading typography needs no Kenaz-specific rule: `base.css` already applies `var(--font-display)`
to `h1`/`h2`, so opting into the type skin is sufficient. (`ignite.css` restates it because it
also changes tracking, not because the base rule is missing.)

### Icons

Regenerated from `mark.svg` at 192, 512, maskable-512 and 32.

Generated by a new `Kenaz.Web/scripts/generate-brand.ps1`, using the Edge headless HTML-to-PNG
method already proven in the og-images workflow: `--headless` (not `--headless=new`, which is
unreliable for `--screenshot`), one `Start-Process -Wait` per render so a fast render cannot
clobber a slow one, and `--default-background-color` to prevent a white band.

The script renders every brand asset — the four icons, the banner and the OG card — from the
SVG sources, so there is one generator rather than two. The existing
`Kenaz.Web/scripts/generate-icons.mjs` is retired: it hand-encodes PNG bytes and cannot draw
bezier paths.

Fonts in generated assets: the banner and OG card wrap their SVG in a small HTML harness that
declares `@font-face` for Fraunces, so the rendered wordmark matches the app exactly rather than
depending on which fonts happen to be installed on the machine. Palatino Linotype and Georgia
stay in the stack as fallbacks.

The `@font-face` src must be a **base64 `data:` URI**, not a path to
`assets/fonts/fraunces-latin-600-normal.woff2`. Headless Chromium treats `file://` origins as
opaque and blocks font fetches across them without `--allow-file-access-from-files`; it does not
error, it silently falls back — so a path-based harness would ship a banner in the wrong
typeface with nothing to indicate it. The script inlines the woff2 at render time
(`[Convert]::ToBase64String`), which is deterministic regardless of origin policy and removes
any dependency on where the design-system sits relative to the script.

### Service worker

`sw.js` line 1 goes from `kenaz-v1` to `kenaz-v2`, in the same commit as the rest of the change.
This is the single most load-bearing line in the whole slice.

The reason is the design-system CSS, not the icons. `public/design-system/**` is served from
stable, unhashed URLs — Vite does not fingerprint anything in `public/` — and every same-origin
non-navigation GET is cached cache-first. Navigations are network-first, so an already-installed
app would fetch the **new** HTML declaring `data-palette="kenaz"` while still holding the
**old 1.0.0** CSS, which has no palette layer at all. `data-palette` would match nothing, the app
would fall back to the default slate, and component styles could break — silently, with no error
anywhere.

The `activate` handler deletes every cache whose key is not the current one, so changing the key
is what flushes the stale CSS wholesale. The stale icons are a real but secondary symptom of the
same mechanism.

Manifest colours stay `#000000` for both `theme_color` and `background_color`, matching
`--surface-1`, which is deliberately true black for OLED. The generated icons therefore use a
`#000000` ground rather than `#0b0a09`, so the icon tile and the Android splash screen agree
instead of showing a faint seam.

### Assets

| File | Size | Purpose |
|---|---|---|
| `docs/brand/mark.svg` | — | Master mark |
| `docs/brand/mark-small.svg` | — | Favicon variant |
| `docs/brand/banner.svg` | 1280×640 | Banner source |
| `docs/brand/banner.png` | 1280×640 | README header and GitHub social preview |
| `Kenaz.Web/public/og.png` | 1200×630 | `og:image`, served from the live site |

Banner design: the mark, the wordmark, a gold rule and the italic tagline *Bring it into the
light*, centred over a flat pine treeline silhouette (`#1a130c`) on `#0b0a09`, inside the
inset amber frame that matches the existing repo-card family. `malinfossum` sits top-left and
`Local-first · Android PWA` top-right.

`index.html` gains the five OG tags — `og:title`, `og:description`, `og:image`, `og:url`,
`og:type` — which it currently has none of. `og:image` and `og:url` must be absolute URLs
(`https://malinfossum.github.io/kenaz/…`); relative paths report as unreachable in validators.
Title targets 50–60 characters and description 110–160, per the og-images standards; the
description carries the local-first and no-account substance rather than filler.

**Constraint on all public copy** — OG tags, repo description, README lead, banner. Check-ins are
mood, energy and sleep, which is health-adjacent data. The claim is device-locality and nothing
more: "stays on your device", "no account, no server". The words **encrypted** and **secure** are
barred, because IndexedDB is plaintext and exports are unencrypted JSON — the app says so itself
on the Data screen, and the public copy must not contradict it.

Validate at opengraph.xyz once deployed. Its *missing call-to-action* and *missing headline in
image* warnings are tuned for marketing landing pages and should be ignored here — adding a
"Sign up" button to this banner would work against the brand.

### README

Rewritten to lead with the shipped product:

1. Banner
2. One-line description and tagline
3. Install — the live link and add-to-home-screen
4. What it does
5. Privacy and data — export/import, and the unencrypted-backup warning
6. The name — kept as written, it is the strongest part of the current README
7. Foundations — the C# core, console and API, noted as the base for the planned desktop app
8. Development — npm, Vitest, build
9. Licence

The `Web app (M6.1)` section is deleted outright. It instructs the reader to paste a bearer
token from an API startup banner into a Setup screen that was removed in `bd8cd61`.

Two constraints on the rewrite:

- The banner image carries real alt text —
  `![Kenaz — bring it into the light](docs/brand/banner.png)` — not an empty or filename alt.
- **No email address appears anywhere in the README.** Contact goes through the GitHub profile.
  This is a from-scratch rewrite, which is exactly where the private address could slip in.

### Repo

- Description rewritten without the ` - ` that currently sits in it.
- Topics added: `pwa`, `local-first`, `offline-first`, `indexeddb`, `wellbeing`, `vanilla-js`,
  `csharp`, `dotnet`.
- Social preview uploaded from `docs/brand/banner.png` — manual, in repo settings. A custom
  preview also mints a fresh `og:image` URL, which clears LinkedIn's stale link-preview cache.

## Repos touched

Two, so two commits — **and the order matters**:

1. **workbench** — `tokens/palettes/kenaz.css` plus its `index.css` import line. This lands first.
2. **kenaz** — the extract upgrade, icons, brand assets, `index.html`, README, and the service
   worker cache bump.

Running extract before the palette exists in workbench produces a silent failure, not an error:
Kenaz gets design-system 2.0.2 without `kenaz.css`, `data-palette="kenaz"` matches no rule, and
the app quietly falls back to the default slate accent while looking otherwise upgraded. Confirm
`Kenaz.Web/public/design-system/tokens/palettes/kenaz.css` exists after extract before going any
further.

### What extract brings with it

`extract.json` includes `assets`, so the sync copies **270.8 KB** — 13 woff2 files and 9 OFL
licence texts — into `Kenaz.Web/public/design-system/assets/`. Only Fraunces (17.7 KB) is ever
downloaded at runtime, because an unused `@font-face` declaration does not fetch. So this is repo
and deploy weight, not user bandwidth, and the licence files travelling alongside the fonts is
required rather than incidental.

The extracted tree is never hand-pruned to slim this down. Doing so breaks both the drift guard
and OFL compliance.

The `@font-face` src paths in `typography.css` are relative (`../assets/fonts/…`), so they
resolve correctly under the `/kenaz/` GitHub Pages base with no rewriting needed.

## Verification

- `npm run build` completes clean.
- `npx vitest run` — 48 tests still green. No domain code is touched, so any failure is a real
  regression.
- No dangling references to the retired `generate-icons.mjs`. Nothing in `package.json` invokes
  it, so retiring it needs no script change, but the grep is cheap and worth doing.
- No `setup` class remains referenced anywhere after the dead CSS is removed.
- `npm run format` is never run repo-wide: it rewrites CRLF to LF across everything, including
  the read-only `public/design-system/**`. Format only the touched files, and undo any stray
  churn with `git checkout -- <paths>`.
- Contrast ratios recomputed against the final palette values, text and non-text separately.
- `Kenaz.Web/public/design-system/tokens/palettes/kenaz.css` exists after extract, and the app's
  computed `--accent` resolves to `rgb(217 154 78)` rather than the slate default.
- Visual pass on all four screens (Today, History, Review, Data) after the design-system
  upgrade, checking for fallout from the major bump rather than assuming compatibility.
- The generated banner renders in Fraunces, not the Palatino fallback — the data-URI check.
  Comparing the wordmark against the app header catches it immediately.
- Icons inspected at 32px and 48px, not only at full size.
- Maskable icon checked against a circular crop.
- Header mark announces once, not twice, under a screen reader.
- No email address anywhere in the rewritten README, and the banner has real alt text.
- Neither "encrypted" nor "secure" appears in the OG tags, repo description or README lead.
- README links resolve and the banner renders on GitHub.
- Final gate, on-device: Malin opens the installed PWA and confirms the new icon **and** the warm
  palette appear. The palette is the real test — it proves the service-worker cache bump flushed
  the old design-system CSS, which is the failure this slice is most exposed to.
