# Spike 05 — Runtime preset swap

**Question:** can the three UI presets (`Od.*` token dictionaries) be swapped at runtime on top of WPF-UI 4.3 without visual artifacts?
**Decision:** GO with conditions. Replacing a code-built preset dictionary in place is fast and showed no visual artifacts. Three conditions apply: any size WPF-UI reads through `StaticResource` goes through our own `Od*` styles, motion is driven from code, and the Windows text size is read from the registry rather than through WinRT.
**Measured:** 2026-10-30 on `spike/p0` at `840c511` (`spikes/ThemeLab`: an `--auto` run, plus a visual check by the maintainer). Issue [#6](https://github.com/poli0981/OmniDeck/issues/6).

## Environment

- Windows Insider build 29648, 32 GB RAM, RTX 5080.
- .NET 10.0.14, WPF-UI 4.3.0.
- Dark theme with the Mica backdrop; text size 100 %, Windows animation effects on, high contrast off.

## Method

ThemeLab is a `FluentWindow` with Mica. It builds each preset's `ResourceDictionary` in code from the [UI-MODES](../UI-MODES.md#2-token-table) table and replaces it in place in `Application.Resources.MergedDictionaries`, after WPF-UI's `ThemesDictionary` and `ControlsDictionary`. The `--auto` run does four things:

1. Under each preset, probes default `ui:Button`, `ui:TextBox` and `ui:Card` controls next to `Od`-styled ones, recording `ActualHeight`, `Padding`, `FontSize` and the template's `CornerRadius`.
2. Times 30 swaps up to `Loaded` dispatcher priority (that is, after layout and render), at 307 and at 3,426 visual elements.
3. Switches Light and Dark with `ApplicationThemeManager.Apply` and checks that the preset layer is still merged.
4. Applies a style whose storyboard uses `{DynamicResource Od.Motion.Duration}`.

The maintainer then switched presets and themes by hand and hovered the motion tile, looking for flicker, Mica glitches and layout jumps. Separately, WPF-UI 4.3's control XAML (tag `4.3.0`) was read to see which keys each control uses through `StaticResource` and which through `DynamicResource`.

## Results

### Which sizes follow a swap

| Control | Dynamic | Standard | Comfort | Follows? |
|---|---|---|---|---|
| default `ui:Button`: padding | 11,5,11,6 | 11,5,11,6 | 11,5,11,6 | no: `ButtonPadding` is a `StaticResource` |
| default `ui:Button`: font size / corner | 13.5 / 8 | 15.5 / 6 | 19 / 6 | yes: `ControlContentThemeFontSize` and `ControlCornerRadius` are dynamic |
| default `ui:Card`: padding | 14,16,14,16 | 14,16,14,16 | 14,16,14,16 | no: `CardPadding` is a `StaticResource` |
| `Od` button (BasedOn `DefaultUiButtonStyle`): height / padding | 32 / 8,4 | 36 / 12,6 | 47 / 20,10 | yes |
| `Od` text box (BasedOn `DefaultUiTextBoxStyle`): height / padding | 32 / 8,4 | 36 / 12,6 | 47 / 20,10 | yes |

The preset layer also redefined `ButtonPadding` and `CardPadding`. WPF-UI's own value won anyway, because those references are resolved inside `ControlsDictionary`. In the 4.3 sources, `ButtonPadding`, `ButtonBorderThemeThickness`, `CardPadding`, `TextBoxBorderThemeThickness` and the text box icon margins are `StaticResource`. `ControlCornerRadius`, `ControlContentThemeFontSize`, `TextControlThemePadding`, `TextControlThemeMinHeight` and `TextControlThemeMinWidth` are `DynamicResource`.

### Swap time (30 swaps each)

| Visual elements | min | median | p90 | max |
|---|---|---|---|---|
| 307 | 6.3 ms | 7.0 ms | 7.8 ms | 18.2 ms |
| 3,426 | 51.3 ms | 62.5 ms | 70.2 ms | 73.3 ms |

The cost is roughly 18 µs per element, so a shell of 1,000–2,000 elements should swap in about 20–40 ms, one or two frames.

### Theme, motion, text size

- **Light/Dark:** `ApplicationThemeManager.Apply` took about 125 ms at 3,426 elements. The preset layer stayed merged both ways, so presets and theme really are independent axes.
- **Motion:** a `DynamicResource` inside a style storyboard throws `InvalidOperationException: Cannot freeze this Storyboard timeline tree for use across threads.` ThemeLab's `OdMotion` behavior avoids this: it reads `Od.Motion.Duration` when each animation starts, skips the animation when the value is 0 or Windows animation effects are off, and follows a swap from the next hover on.
- **Text size:** WPF ignores the Windows text size setting. It can be read from two places, and both returned 1.00 here:
  - the registry, `HKCU\Software\Microsoft\Accessibility\TextScaleFactor` (a percentage; missing means 100);
  - WinRT, `UISettings.TextScaleFactor`. This needs a Windows-versioned target framework, which puts `Microsoft.Windows.SDK.NET.dll` (26.3 MB) in the output.
- **Visual check (maintainer):** no flicker, Mica glitch or layout jump when switching presets or themes, and the motion tile behaved as expected.

## Consequences

- **Theme engine (P1):** build the preset dictionary in code, since it needs the OS text scale and the user's overrides anyway. Swap that one merged dictionary in place, and keep WPF-UI's dictionaries first.
- **`Od` styles:** for each control that reads a size through `StaticResource`, ship an implicit style BasedOn the WPF-UI default with `DynamicResource` setters bound to `Od.*` tokens. Dynamic WPF-UI keys can be overridden directly in the preset layer, with a comment on each override ([WPFUI-CONVENTIONS](../WPFUI-CONVENTIONS.md#6-theming-with-od-tokens) §6).
- **Motion:** no token-driven storyboards in XAML styles. Animations go through a small behavior that reads `Od.Motion.*` when it starts and honors `SystemParameters.ClientAreaAnimation`.
- **Text size:** read the registry value at startup, and read it again on `WM_SETTINGCHANGE` and whenever the window is activated. This needs no Windows-versioned target framework, which saves about 26 MB. This spike did not observe which notification fires on a live change; re-reading on activation covers both cases.
- **Budget:** swaps only happen when the user picks a preset, so 20–70 ms is fine; keep the shell's visual tree lean. High Contrast and text sizes above 100 % are checked in the P3 accessibility pass.

## Reproduce

On `spike/p0`:

```powershell
dotnet run --project spikes/ThemeLab -- --auto   # log in %LocalAppData%\OmniDeck.Spikes\theme\
dotnet run --project spikes/ThemeLab             # interactive
```
