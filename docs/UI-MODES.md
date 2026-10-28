# OmniDeck — UI Presets & Design Tokens

One theme engine, three presets. Presets are *bundles of token values*, not separate UIs — every screen reads tokens from merged `ResourceDictionary` layers (base Fluent theme via WPF-UI → preset dictionary → user overrides), swapped at runtime.

## 1. Naming

Presets are named by their character, never by age, both in the UI and in code:

- **Dynamic** — dense, vivid, animated (skews younger users).
- **Standard** — the default, balanced.
- **Comfort** — large type, high contrast, calm (skews older users and anyone who prefers it).

## 2. Token table

| Token | Dynamic | Standard | Comfort |
|---|---|---|---|
| `Od.Font.Base` | 13.5 px | 15.5 px | 19 px |
| `Od.Font.Scale` (h1/h2 ratio) | 1.35 | 1.30 | 1.25 |
| `Od.Density` (control padding unit) | 4 px | 6 px | 10 px |
| `Od.HitTarget.Min` | 32 px | 36 px | 44 px |
| `Od.Corner.Radius` | 8 px | 6 px | 6 px |
| `Od.Motion` | full (150–200 ms) | reduced | off |
| `Od.Contrast.Target` | WCAG AA (4.5:1) | AA | AAA body text (7:1) |
| `Od.Accent.Saturation` | high, gradient accents allowed | medium | medium, no gradients |
| `Od.Nav.MaxVisibleItems` | unlimited | unlimited | 6 |
| `Od.Nav.IconLabels` | icons, labels on hover | icons + labels | icons + labels, larger |
| `Od.Chart.LineWeight` | 1.5 px | 2 px | 3 px |

Rules for module authors: never hardcode a font size, padding, radius, or animation duration — consume tokens. CI includes a XAML lint pass (XamlStyler + a grep rule) that flags literal `FontSize=` in module views.

## 3. User overrides

Appearance settings expose the presets as three cards plus granular overrides on top: font-size slider (0.85–1.4× of preset base), motion on/off, accent color. Overrides persist independently of preset choice, so switching presets keeps the user's font preference.

## 4. Windows accessibility interplay (non-negotiable)

- **Text scaling:** the OS text scale factor multiplies `Od.Font.Base` on top of everything else.
- **High Contrast:** when `SystemParameters.HighContrast` is on, palette tokens defer to system colors entirely; only layout tokens from the preset remain.
- **Reduced motion:** if the OS animation setting is off, `Od.Motion` is forced off regardless of preset.

## 5. Onboarding

Immediately after the legal gate, first-run shows: language (EN/VI/JA) → preset picker (three live preview cards rendered with each preset's actual tokens — no screenshots) → done. Both choices are one click to change later in Settings.

## 6. Dark/light

Independent axis from presets: Light / Dark / Follow Windows (default). Every preset defines both palettes; contrast targets in §2 apply to both.
