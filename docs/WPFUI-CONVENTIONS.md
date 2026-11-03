# OmniDeck — WPF-UI Conventions

How we use **WPF-UI (lepoco/wpfui)** in this codebase. Pinned version: **4.3.0** (verified 2026-10-28). License: MIT — reproduced in [THIRD-PARTY-NOTICES](legal/THIRD-PARTY-NOTICES.md), which ships with the app. When docs/tutorials found online disagree with this file, prefer this file: much of the web still teaches v2/v3 APIs.

## 1. Packages

| Package | When |
|---|---|
| `WPF-UI` | always (pulls `WPF-UI.Abstractions` automatically) |
| `WPF-UI.DependencyInjection` | Settings page navigation via DI (`AddNavigationViewPageProvider`) |
| `WPF-UI.Tray` | only if the P1 tray evaluation picks it over H.NotifyIcon — never both |

## 2. Bootstrap

`OdApp.xaml` — WPF-UI dictionaries first, then our preset layer (merge order is load-bearing; see [UI-MODES](UI-MODES.md)). The application class is `OdApp`, not `App`: our types carry the `Od` prefix, and Meziantou MA0049 rejects a type named like its namespace (`OmniDeck.App.App`). `Program.Main` runs `VelopackApp.Build().Run()` before WPF starts, then creates `OdApp`.

```xml
<Application
    x:Class="OmniDeck.App.OdApp"
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

Rules: top-level windows are `ui:FluentWindow` (module pop-outs included); exactly one `ui:TitleBar` per window; Mica falls back to a solid background when Windows transparency effects are off or a contrast theme is active — that state must look intentional, so test both.

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

- Module views consume **`Od.*` tokens only** (fonts, spacing, radius, motion — see [UI-MODES](UI-MODES.md)). CI greps every view for a literal `FontSize`, and for a literal `Margin`, `Padding` or `CornerRadius` other than zero.
- Text size reaches views through property inheritance, and three things in WPF-UI 4.3 cut it off:
  - the navigation view's frame passes neither the text color nor the size into its pages, so each page and the module stage set `Foreground` and `FontSize` themselves;
  - `ListBox` is styled at 14 px and `ToolTip` at 12 px, so `OdStyles.xaml` restyles both from the tokens;
  - the sidebar item template sets 14 px on its label, so the shell gives its items a label template of its own (`NavLabel`).
- Colors come from WPF-UI theme resources via `DynamicResource` (e.g. `TextFillColorPrimaryBrush`, `ControlFillColorDefaultBrush` — look keys up in the Gallery/theme dictionaries, don't guess). Never hardcode a brush.
- Preset dictionaries may override WPF-UI resource keys **deliberately and documented** — each override gets a comment stating why.
- Accent color (user override in Appearance settings) goes through `ApplicationAccentColorManager` — one call site in the theme engine, nowhere else.
- Dark/light is an axis independent of presets; both palettes must pass the contrast targets in UI-MODES for every preset.

## 7. Accessibility

What screen readers and the keyboard need, and what WPF-UI 4.3 leaves out (P3; `scripts/od-drive.ps1 -Action audit` lists what is still missing on a screen):

- **Sidebar.** WPF-UI hosts the items in plain item controls, whose peers wrap each item in a data item that can be neither invoked nor selected. `OdNavigationView`'s peer lists `OdNavItem`s instead, as two lists (the modules, then Settings and the library) of list items with Invoke and SelectionItem. Both are subclasses, so they take WPF-UI's styles by name; a test lays the view out on WPF-UI's own dictionaries to catch a template that changes.
- **Template parts.** `OdAutomationNames` names the buttons WPF-UI leaves unnamed, or named after a glyph such as "CaretUp24": the title bar's (Maximize turns into Restore), the sidebar's pane button (Open or Close navigation), the scroll bar, slider and number box buttons, and the password box's reveal button. An editable combo box's text box, which takes the focus, gets its combo box's name. It names each on its first layout: WPF raises `Loaded` only on elements that handle it themselves.
- **Lists.** Screen readers name a list item after its data's `ToString()`, which for a record lists all its members: every item type overrides it, as a sentence where the row shows several values. A button that every row has is named after its row too, with `{res:OdLocRow Key, Path=…}` ("Remove, Tokyo"; `LabelPath=` when the row provides the label).
- **State.** A button whose job changes is named for what it does now (Play or Pause, Mute or Unmute), and so is its tooltip.
- **Headings and live regions.** Page titles are `AutomationProperties.HeadingLevel="Level1"` and section titles `Level2`. A status line is an `OdStatusText`: WPF knows `LiveSetting`, but makes a change heard only when the element raises the event, which a `TextBlock` never does.
- **Keyboard.** Ctrl+K opens the palette in pop-outs too, where Ctrl+W closes the window; the palette and Ctrl+1–9 move the focus into the page they open. Enter and Delete act on the selected item of the media queue and the calculator's history. In a web app the shell's shortcuts come first: WebView2's WPF control hands Ctrl and Alt keys to WPF before the page sees them, so Ctrl+K opens the palette rather than, say, Discord's quick switcher.

## 8. Icons

WPF-UI ships Fluent System Icons (MIT). **Segoe Fluent Icons cannot be redistributed** (font EULA); it is present on every supported Windows 11 build, but we still stick to `SymbolRegular`/`SymbolFilled` glyphs so the UI never depends on a system font version. We do not bundle the font.

## 9. Hard rules

- Never mix WPF-UI with Material Design In XAML (or any second UI kit) in this app.
- `ui:` controls subclass their native counterparts — code and tests targeting base types (`ButtonBase`, `TextBoxBase`) keep working; don't duplicate logic per control flavor.
- XamlStyler formats everything, including `ui:` attributes (upstream uses XamlStyler themselves).
- High Contrast: when active, WPF-UI/system colors win over presets ([UI-MODES](UI-MODES.md) §4). Colors come from theme keys only (the charts included); `Od.Opacity.Dim` is 1 there, and the accent swatches are hidden.
- `FluentWindow` chrome + child-HWND content (LibVLCSharp `VideoView`): fullscreen passed P0 spike #6, and media pop-out passed in P2 by moving the same view, whose overlay must not be owned by a closing window ([ARCHITECTURE](ARCHITECTURE.md) §2).
- Version bumps of `WPF-UI*` packages are Dependabot PRs like any other, but read the release notes first — v3→v4 proved this library will rename APIs between majors.
