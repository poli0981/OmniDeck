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

Rules for module authors: never hardcode a font size, padding, margin, radius, or animation duration — consume tokens. CI includes a XAML lint pass (XamlStyler + a grep rule over every view in `src`) that flags a literal `FontSize`, and a literal `Margin`, `Padding` or `CornerRadius` with any value other than zero.

As built (P1, then P3), every token in the table drives the app except `Od.Accent.Saturation`:

- **Sidebar.** Dynamic closes the sidebar to icons, with each name in a tooltip; Comfort shows at most six modules, always including the one on screen, and the rest stay in the library and the palette. `Od.Nav.IconLabels` is two tokens, `Od.Nav.ShowLabels` and `Od.Nav.IconSize` (16 px; 22 px in Comfort); labels are at `Od.Font.Base`, and items at least `Od.HitTarget.Min` tall.
- **Spacing.** It all comes from `Od.Density`: `Od.Padding.Control` and `Od.Padding.Card`, and the margins `Od.Margin.Item` (after a block), `Od.Margin.Tile` (after a tile in a grid), `Od.Margin.Leading` and `Od.Margin.Inline` (before and after an element in a row) and `Od.Margin.Tight` (between the keys of a keypad).
- **Contrast.** `Od.Contrast.Target` picks the preset's text palette (§6). Secondary and tertiary text meet 4.5:1 in Dynamic and Standard, and 7:1 in Comfort, on every WPF-UI surface of the light and dark themes; WPF-UI's own tertiary text is about 3.3:1 on the light theme. A test reads the surfaces from WPF-UI's theme dictionaries, so an update of WPF-UI is checked too.
- **Hit targets.** `Od.HitTarget.Min` is the minimum height of buttons, text boxes, check boxes, radio buttons, combo boxes, toggle switches, list items, menu items and sidebar items.
- **No gradients.** Dynamic's gradient accents were cut in P3: text over a gradient changes contrast across the surface, so no preset draws one and `Od.Accent.Saturation` is not a token. Every preset uses the accent Windows or the user chose.
- **Charts.** `Od.Chart.LineWeight` sets the line width of the charts (Weather, Stocks).

## 3. User overrides

Appearance settings expose the presets as three cards plus granular overrides on top: font-size slider (0.85–1.4× of preset base), motion on/off, accent color. Overrides persist independently of preset choice, so switching presets keeps the user's font preference.

## 4. Windows accessibility interplay (non-negotiable)

- **Text scaling:** the OS text scale factor multiplies `Od.Font.Base` on top of everything else. As built, widths that hold text grow with it too: `Od.Width.Tile`, `Od.Width.TileWide`, `Od.Width.Field` and `Od.Width.Readable` are multiples of `Od.Font.Base`, and rows of buttons in tiles wrap rather than clip.
- **High Contrast:** when `SystemParameters.HighContrast` is on, palette tokens defer to system colors entirely; only layout tokens from the preset remain. As built, the preset layer then carries no text palette, `Od.Opacity.Dim` (0.7 otherwise, for what is dimmed but still in use) is 1, and Settings hides the accent swatches.
- **Reduced motion:** if the OS animation setting is off, `Od.Motion` is forced off regardless of preset.

## 5. Onboarding

Immediately after the legal gate, first-run shows: language (EN/VI/JA) → preset picker (three live preview cards rendered with each preset's actual tokens — no screenshots) → done. Both choices are one click to change later in Settings.

As built, each card gets that preset's own layer, built by the theme engine at the Windows text size, the user's font scale and the current theme (`OdPresetPreview`), so its title and text show the preset's sizes and text palette. Picking a card applies the preset to the whole window at once.

## 6. Dark/light

Independent axis from presets: Light / Dark / Follow Windows (default). Every preset defines both palettes; contrast targets in §2 apply to both.

As built, a preset's palette replaces WPF-UI's secondary and tertiary text brushes (`OdPalette`); primary text and every other color stay WPF-UI's. Dynamic and Standard keep WPF-UI's values where they already pass and darken the light theme's tertiary text; Comfort's are stronger on both themes. In High Contrast the preset layer carries no palette at all.
