# OmniDeck — WPF-UI Conventions

How we use **WPF-UI (lepoco/wpfui)** in this codebase. Pinned line: **4.3.0+** (May 2026). License: MIT — copy shipped in `licenses/` per THIRD-PARTY-NOTICES. When docs/tutorials found online disagree with this file, prefer this file: much of the web still teaches v2/v3 APIs.

## 1. Packages

| Package | When |
|---|---|
| `WPF-UI` | always (pulls `WPF-UI.Abstractions` automatically) |
| `WPF-UI.DependencyInjection` | Settings page navigation via DI (`AddNavigationViewPageProvider`) |
| `WPF-UI.Tray` | only if the P1 tray evaluation picks it over H.NotifyIcon — never both |

## 2. Bootstrap

`App.xaml` — WPF-UI dictionaries first, then our preset layer (merge order is load-bearing; see docs/UI-MODES.md):

```xml
<Application
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ui:ThemesDictionary Theme="Dark" />
        <ui:ControlsDictionary />
        <!-- OmniDeck token presets layer on top: -->
        <ResourceDictionary Source="Themes/Preset.Standard.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

Every window/view that uses library controls declares the same `xmlns:ui` URI. Runtime theme operations:

```csharp
ApplicationThemeManager.Apply(ApplicationTheme.Light);   // dark/light axis
SystemThemeWatcher.Watch(mainWindow);                    // "Follow Windows" mode
```

## 3. Shell window pattern

```xml
<ui:FluentWindow
    x:Class="OmniDeck.App.OdMainWindow"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    ExtendsContentIntoTitleBar="True"
    WindowBackdropType="Mica">
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <ui:TitleBar Title="OmniDeck" Grid.Row="0" />
    <!-- shell content -->
  </Grid>
</ui:FluentWindow>
```

Rules: top-level windows are `ui:FluentWindow` (module pop-outs included); exactly one `ui:TitleBar` per window; **Mica renders on Windows 11 only** — the Win10 fallback (solid background) must look intentional, so test both.

## 4. Control mapping (native → ours)

Prefer the `ui:` control when one exists; plain native controls are fine where no `ui:` variant exists — `ControlsDictionary` restyles them to Fluent anyway.

| Traditional WPF | Use instead | Notes |
|---|---|---|
| `Window` | `ui:FluentWindow` | title bar + backdrop |
| `Button` | `ui:Button` | `Icon="{ui:SymbolIcon Play24}"` |
| `TextBox` | `ui:TextBox` | `PlaceholderText`, leading icon |
| `PasswordBox` | `ui:PasswordBox` | reveal button (Twelve Data key field) |
| numeric input | `ui:NumberBox` | governor thresholds, timer digits |
| search field | `ui:AutoSuggestBox` | command palette, ticker search |
| `CheckBox` on/off | `ui:ToggleSwitch` | settings toggles |
| status banner | `ui:InfoBar` | "stale weather data", provider errors |
| grouping `Border` | `ui:Card` / `ui:CardControl` | settings rows, module tiles |
| transient message | Snackbar via `ISnackbarService` | never `MessageBox` for non-blocking info |
| modal dialog | `ContentDialog` via `IContentDialogService` | `MessageBox` only for pre-UI fatal errors |
| busy indicator | `ui:ProgressRing` | |
| glyphs | `ui:SymbolIcon` (+`SymbolRegular`/`Filled` enums) | never PNGs for UI glyphs |
| `ComboBox`, `Slider`, `DataGrid`, `Menu` | keep native | restyled by the dictionary |

Snackbar/dialog services need a presenter host registered from the shell window at startup — copy the wiring from the current `Wpf.Ui.Gallery` sample rather than old blog posts.

## 5. Navigation — two distinct uses

1. **Module host (main sidebar):** `ui:NavigationView` is *chrome only*. Item invocations route to `OdModuleHost` (activate/suspend per the governor); we do **not** use page-based navigation for modules — their lifecycle is ours, not the control's.
2. **Settings pages:** normal page navigation is fine. v4 style: register pages in DI, call `services.AddNavigationViewPageProvider()` (from `WPF-UI.DependencyInjection`), hand it to the view via `SetPageProviderService(...)`. v4 renamed `IPageService` → `INavigationViewPageProvider`, and `INavigationAware`/`INavigableView` now live in `Wpf.Ui.Abstractions.Controls`; navigation callbacks are async-oriented — don't block them.

## 6. Theming with Od.* tokens

- Module views consume **`Od.*` tokens only** (fonts, spacing, radius, motion — see UI-MODES). CI greps for literal `FontSize=` in module views.
- Colors come from WPF-UI theme resources via `DynamicResource` (e.g. `TextFillColorPrimaryBrush`, `ControlFillColorDefaultBrush` — look keys up in the Gallery/theme dictionaries, don't guess). Never hardcode a brush.
- Preset dictionaries may override WPF-UI resource keys **deliberately and documented** — each override gets a comment stating why.
- Accent color (user override in Appearance settings) goes through `ApplicationAccentColorManager` — one call site in the theme engine, nowhere else.
- Dark/light is an axis independent of presets; both palettes must pass the contrast targets in UI-MODES for every preset.

## 7. Icons on Windows 10

WPF-UI ships Fluent System Icons (MIT). **Segoe Fluent Icons cannot be redistributed** (font EULA): present by default on Windows 11 only. Rule: stick to `SymbolRegular`/`SymbolFilled` glyphs. If a Segoe-only glyph ever becomes necessary, it needs a Win10 fallback glyph — we do not bundle the font.

## 8. Hard rules

- Never mix WPF-UI with Material Design In XAML (or any second UI kit) in this app.
- `ui:` controls subclass their native counterparts — code and tests targeting base types (`ButtonBase`, `TextBoxBase`) keep working; don't duplicate logic per control flavor.
- XamlStyler formats everything, including `ui:` attributes (upstream uses XamlStyler themselves).
- High Contrast: when active, WPF-UI/system colors win over presets (UI-MODES §4) — verify in the P3 accessibility pass.
- `FluentWindow` chrome + child-HWND content (LibVLCSharp `VideoView`): fullscreen toggle is part of P0 spike #6; don't ship media pop-out before it passes.
- Version bumps of `WPF-UI*` packages are Dependabot PRs like any other, but read the release notes first — v3→v4 proved this library will rename APIs between majors.
