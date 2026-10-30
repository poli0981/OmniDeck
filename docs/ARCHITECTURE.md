# OmniDeck — Architecture

Everything is a **module** with a governed lifecycle. The shell is a thin host: navigation, theme engine, settings, legal gate, and the two subsystems that make the concept work — the **web runner** and the **RAM governor**.

## 1. Solution layout

```
OmniDeck.slnx
Directory.Build.props  shared build settings: nullable, warnings as errors, analyzers,
                       NuGet Audit, product metadata and dev-build version
global.json            pinned .NET SDK
.config/               dotnet-tools.json: vpk, dotnet-sonarscanner, XamlStyler
scripts/               pack.ps1 (publish + Velopack), validate-catalog.ps1
src/
  OmniDeck.App/        WPF shell: windows, navigation, tray and notifications, command
                       palette, theme engine, legal gate, onboarding, settings UI,
                       task-manager panel, update service (Velopack)
  OmniDeck.Core/       contracts (IOdModule, IOdNotifier, IOdQuoteProvider, ...), module
                       host, RAM governor, storage (SQLite/Dapper, settings), theme
                       tokens, legal acknowledgment, bug-report composer, i18n helpers
  OmniDeck.Modules/    built-in mini-apps: Clock, Timer, Calculator, Weather,
                       Calendar, Stocks, Media — one folder per module   (P1)
  OmniDeck.Web/        web runner: environment/profile management, catalog loader,
                       navigation policy, notifications bridge, permissions,
                       downloads, CDP console capture                     (P2)
tests/
  OmniDeck.Tests/      xUnit v3; governor state machine, providers, catalog validation,
                       scrubber, rate limiter
catalog/
  webapps.json         curated web app catalog (also fetched from raw.githubusercontent)
  webapps.schema.json  its JSON Schema (CI validation, editor completion)
```

State at kickoff: `OmniDeck.App` (Velopack bootstrap + a placeholder `FluentWindow`), `OmniDeck.Core` (`OdVersionInfo`) and `OmniDeck.Tests` exist. `OmniDeck.Modules` and `OmniDeck.Web` are created when their first real code lands (P1/P2), so the build never carries empty projects. `OmniDeck.Modules` arrived in P1 with Clock, Timer and Calculator: it references Core (and WPF-UI for its views) but never the app, registers its modules with `AddOmniDeckModules()`, and keeps its own string table. Durable module state lives outside the views, in a service (`OdTimerService`), a view model the module keeps (the calculator's history), or the module's settings document (the clock's zones), so hibernating a view loses nothing.

Naming: namespace `OmniDeck.*`, public type prefix `Od*` (`OdModuleHost`, `OdRamGovernor`, `OdWebRunner`, `OdCatalogEntry`), interfaces `IOd*`. The WPF application class is `OdApp` (`OdApp.xaml`), started from `Program.Main` after Velopack's hooks; the executable is `OmniDeck.exe`.

Patterns: MVVM via CommunityToolkit.Mvvm; constructor DI via `Microsoft.Extensions.DependencyInjection`; no service locator.

Startup, as built in P1: `Program.Main` runs Velopack's hooks first (the uninstall hook deletes the data root), then the single-instance guard: a session-local mutex, and a second launch signals the first to show its window and exits. `OdApp` builds the container (`ServiceCollection` with `ValidateOnBuild`), installs the crash handler and starts the theme engine. It then shows the legal gate when the acknowledged legal version is not the current one, and onboarding on first run. Next it creates the main window, which owns the tray icon and therefore exists even when Windows starts OmniDeck in the tray (`--minimized`). Last come the governor and the daily update check. Shutdown is explicit: closing the window hides it in the tray, and only Quit ends the process. Notifications are tray balloons behind `IOdNotifier`, which Windows 11 shows as toasts, so the app needs no Windows-specific target framework or WinRT projection. The Velopack update service lives in the app, because Core must not reference Velopack.

## 2. Module contract

```csharp
public enum OdRamClass { Light, Heavy }          // governor priority hint
public enum OdModuleState { Created, Active, Idle, Suspended, Hibernated, Closed }

public interface IOdModule
{
    OdModuleInfo Info { get; }                   // Id, NameKey, Icon, CategoryKey, RamClass
    FrameworkElement CreateView();               // called on Activate after hibernation
    Task InitializeAsync(IOdModuleContext ctx);  // once per process lifetime
    Task ActivateAsync();                        // became visible
    Task<bool> CanSuspendAsync();                // e.g. media playing => false (pinned)
    Task SuspendAsync(OdSuspendReason reason);   // hidden, or governor pressure
    Task HibernateAsync();                       // release *everything*; keep tiny state blob
    Task CloseAsync();                           // user closed the module
}
```

`IOdModuleContext` gives modules their own settings document (one JSON row per module in the `module_settings` table), the notifier (tray balloons, which Windows 11 shows as toasts), a logger and the clock; the HTTP client factory joins it with the first module that goes online (P2). Modules never construct these themselves.

`CreateView()` returns a WPF `FrameworkElement`, so wherever `IOdModule` lives must reference WPF. P1 decided to let `OmniDeck.Core` take that dependency (`UseWPF`) rather than add an abstractions project. Core holds the contract, `OdModuleHost` and the localization layer (`OdLocalization`, `{res:OdLoc Key}`), so modules depend on Core alone; Core still never references the app, Velopack or WPF-UI (an architecture test checks it), which is why `OdModuleInfo.Icon` is a `SymbolRegular` name rather than a symbol.

`OdModuleHost` runs the contract. It initializes a module on first open, builds its view when it has none (first open, or after hibernation), keeps the view of the visible module, and moves hidden ones down the ladder when the shell or the governor asks; it refuses to suspend or hibernate a module whose `CanSuspendAsync` returns false. Calls run one at a time on the UI thread. The shell's navigation view is chrome only: every module item navigates to one shared stage page, whose content is the view the host hands back, so the control never creates or caches module views.

Native modules run in-process. True RAM release for them means honoring `SuspendAsync`/`HibernateAsync`: dispose charts, bitmaps, timers, caches; the governor follows up with a GC. Web modules get real process-level release for free because WebView2 is out-of-process by design.

## 3. Web runner internals

- **Environment:** one `CoreWebView2Environment` for the whole app → browser/GPU processes are shared across web apps. Created lazily on first web app open.
- **Profiles:** `CoreWebView2Profile` per catalog `profile` value. `google` is shared (one sign-in for Gmail/Calendar/Drive/Maps/YouTube); every other app is isolated. Cookies live in the profile, which is what lets **hibernate = full dispose** without losing sessions.
- **Per-app pipeline on creation:** apply settings (`AreDevToolsEnabled=false` in Release, `IsPasswordAutosaveEnabled=false`, `IsGeneralAutofillEnabled=false`) → attach `NavigationStarting` (navAllow suffix check) → `NewWindowRequested` (popupAllow → in-app popup on same profile; else default browser) → `PermissionRequested` → `NotificationReceived` → `DownloadStarting` → start CDP console capture (`Runtime.enable`, `Log.enable`).
- **Suspend path:** hide the control → `TrySuspendAsync()` → set `MemoryUsageTargetLevel = Low`. **Resume:** set target `Normal`, show; WebView2 resumes automatically on visibility.
- **Hibernate path:** persist `{ url }`, `Dispose()` the control and `CoreWebView2`. Reopen = new control on the same profile navigating to the saved URL.
- **Catalog refresh:** at most daily, fetch `catalog/webapps.json` from this repo's raw URL over HTTPS; validate against schema version; on any validation failure keep the bundled copy.

## 4. RAM governor (summary — full spec in RAM-GOVERNOR.md)

A single scheduler owns all state transitions. Inputs: per-module visibility + last-interaction time, pin status (`CanSuspendAsync` / `IsDocumentPlayingAudio`), and total footprint = private commit (`Process.PrivateMemorySize64`) of the shell plus the WebView2 processes from `CoreWebView2Environment.GetProcessInfos()`. Policies: idle timers demote Active→Idle→Suspended→Hibernated; soft cap demotes LRU modules early; hard cap force-hibernates and toasts. The task-manager panel is a read/write view over the same scheduler (manual suspend/kill). In code: `OdRamGovernor` in `OmniDeck.Core/Governor`, polling every 10 s on the UI thread through `OdModuleHost`, behind `IOdMemory` so tests drive it with a fake gauge and `FakeTimeProvider`.

## 5. Storage

All app data lives under the **data root** `%LocalAppData%\OmniDeck.Data`:

- `omnideck.db` — SQLite via Dapper. Tables per module (`calendar_events`, `stocks_watchlist`, `stocks_quotes_cache`, `weather_cache`, `media_playlists`, …) plus `app_meta` (legal acknowledgment, schema version). Migrations = ordered idempotent SQL scripts executed at startup.
- `settings.json` — typed settings tree; import/export from the Data settings page.
- `WebView2\` — the WebView2 user data folder (profiles, cookies, caches).
- `logs\`, `reports\` — Serilog rolling files and bug-report zips.
- Secrets: only the optional Twelve Data key, encrypted with DPAPI (`ProtectedData`, CurrentUser scope).

Why not `%LocalAppData%\OmniDeck`: that is Velopack's install folder (packId `OmniDeck`). Velopack deletes it entirely on uninstall and when Setup is run again over an existing install, so nothing the user owns may live there. The data root is a sibling folder that survives updates and reinstalls; the Velopack uninstall hook (`VelopackApp.Build().OnBeforeUninstallFastCallback`, P1) deletes it on uninstall, as PRIVACY promises. It is local, not roaming, because WebView2 caches can reach several GiB.

## 6. Logging & failure handling

Serilog rolling files (7-day retention). Global handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) log, show a friendly dialog, and offer the bug-report flow. `CoreWebView2.ProcessFailed` triggers an automatic single reload of that app before surfacing an error tab.

## 7. Updates & packaging

Velopack, per-user, GitHub Releases channel, delta updates; update check on About open and once per day in the background (notify-only, never auto-restart). Builds below 1.0 are GitHub pre-releases, so their update check includes pre-releases. In code: `OdVelopackUpdateService` behind `IOdUpdateService`; Settings → About checks when opened and offers *Download and restart*, and `OdUpdateWatcher` checks a few minutes after start, then daily, and notifies once per new version. A development build is not installed, so it does neither.

Releases are built by [`release.yml`](../.github/workflows/release.yml), self-contained in this repository: a GPG-signed `vX.Y.Z[-suffix]` tag on `main` runs `scripts/pack.ps1` (self-contained `win-x64` publish + `vpk pack`), build-provenance attestations, a draft GitHub Release with an SPDX SBOM and SHA-256 checksums, release notes from CHANGELOG, publication, and a Discord announcement — see [RELEASING](../RELEASING.md). The WebView2 Runtime ships with Windows 11; if it is ever missing, Velopack's `--framework webview2` bootstrapper can install it (evaluate in P2).
