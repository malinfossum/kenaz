# Kenaz brand assets

The **Lantern** identity: a faceted frame protecting a private reflection, with a
central flame that reveals patterns without judging them. Cool blue-grey light on a
true-black ground — *held light, private reflection.*

## Files

| File | What it is |
|---|---|
| `mark.svg` | The full emblem. Use at 128 px and above. |
| `mark-small.svg` | Compact emblem — heavier strokes, no inner detail. Use below 128 px, and for the app header. |
| `banner.png` / `.svg` | 1280×640 lockup. Leads the README and doubles as the repo social preview. |
| `avatar.png` | 800×800 square, for a GitHub org/profile avatar. |
| `logos/horizontal.svg` | Mark + wordmark + tagline, side by side. |
| `logos/stacked.svg` | Mark above the wordmark. |
| `logos/wordmark.svg` | Wordmark alone, outlined so it needs no font. |
| `logos/app-icon.svg` | The emblem on its rounded app-icon tile. |

The shipped app icons live in `Kenaz.Web/public/icons/` and the Open Graph card at
`Kenaz.Web/public/og.png`, since both are served with the app rather than read from here.

## Colour

The mark uses the Kenaz palette directly, so it sits on the app without adjustment:

| Role | Value | Token |
|---|---|---|
| Ground | `#000000` | `--surface-1` |
| Panel | `#0A0D10` | `--surface-2` |
| Frame | `#7C9AB3` | `--accent` |
| Flame | `#90ADC5` | `--accent-strong` |
| Flame core | `#F4F7FA` | `--text` |

These are the same values `data-palette="kenaz"` sets in the design-system, which is why
`docs/brand/mark.svg` can hardcode them while the header mark in `src/view/view.js` reads
the tokens instead — the header has to follow the light theme, a static file does not.

## Type

Sora SemiBold for the wordmark and headings, Figtree for body text — both inherited from
the design-system's `:root`, so the app declares no `data-typeskin`. The wordmark in
`logos/wordmark.svg` is outlined, so it renders correctly without either font installed.

## Regenerating

These are vendored finals, not build output — there is no generator script. Edit the SVG
in place, or take it back to the design tool that produced it, then re-export the rasters
at the sizes listed in `Kenaz.Web/public/icons/`. Android crops maskable icons to a circle
at roughly 80% of the tile, which is why `icon-maskable-*.png` sits the emblem smaller in
its frame than `icon-*.png` does.
