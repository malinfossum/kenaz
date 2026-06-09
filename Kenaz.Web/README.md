# web-vite

Module-based MVC starter, Vite + Biome. Use this for personal projects and anything that benefits from a build step.

## What's included

- Full design system (`design-system/`) — tokens, primitives, components, compositions, utilities, theme
- No-flash dark/light theme toggle (works on first load, persists in `localStorage`)
- Mobile-first responsive baseline
- Accessibility defaults (focus rings, reduced-motion, forced-colors, skip link)
- Biome (formatter + linter + import organizer)

## First 5 steps

1. `npm install`
2. Set `<title>` and `<meta name="description">` in `index.html`
3. Wire `createModel` / `createView` / `createController` in `src/app.js`
4. Add project-specific styles in `src/styles/main.css` (mobile-first, no `max-width` queries)
5. `npm run dev` and start building

## Scripts

```bash
npm run dev            # start dev server
npm run build          # build for production
npm run preview        # preview the build

npm run format         # format files in place
npm run format:check   # report files that would be reformatted
npm run lint           # run linter
npm run check          # format + lint + organize imports (write changes)
```

## Folder layout

- `index.html` — app shell, contains `<main id="main">`
- `src/main.js` — boots the app
- `src/app.js` — wires `createModel` / `createView` / `createController`
- `src/model/`, `src/view/`, `src/controller/` — MVC layers
- `src/styles/main.css` — project-specific overrides
- `design-system/` — read-only foundation, do not edit
- `biome.json` — formatter and linter config
- `vite.config.js` — Vite config
