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

State at kickoff: `OmniDeck.App` (Velopack bootstrap + a placeholder `FluentWindow`), `OmniDeck.Core` (`OdVersionInfo`) and `OmniDeck.Tests` exist. `OmniDeck.Modules` and `OmniDeck.Web` are created when their first real code lands (P1/P2), so the build never carries empty projects. `OmniDeck.Modules` arrived in P1 with Clock, Timer and Calculator: it references Core (and WPF-UI for its views) but never the app, registers its modules with `AddOmniDeckModules()`, and keeps its own string table. `OmniDeck.Web` arrived in P2 the same way: it references Core, the WebView2 SDK and WPF-UI but never the app or `OmniDeck.Modules`, and `AddOmniDeckWeb(catalog, options)` registers the catalog service, the runtime and one module per catalog entry, after the built-in modules. Durable module state lives outside the views, in a service (`OdTimerService`), a view model the module keeps (the calculator's history), or the module's settings document (the clock's zones), so hibernating a view loses nothing.

Naming: namespace `OmniDeck.*`, public type prefix `Od*` (`OdModuleHost`, `OdRamGovernor`, `OdWebRunner`, `OdCatalogEntry`), interfaces `IOd*`. The WPF application class is `OdApp` (`OdApp.xaml`), started from `Program.Main` after Velopack's hooks; the executable is `OmniDeck.exe`.

Patterns: MVVM via CommunityToolkit.Mvvm; constructor DI via `Microsoft.Extensions.DependencyInjection`; no service locator.

Startup, as built in P1: `Program.Main` runs Velopack's hooks first (the uninstall hook deletes the data root), then the single-instance guard: a session-local mutex, and a second launch signals the first to show its window and exits. `OdApp` builds the container (`ServiceCollection` with `ValidateOnBuild`), installs the crash handler and starts the theme engine. It then shows the legal gate when the acknowledged legal version is not the current one, and onboarding on first run. Next it creates the main window, which owns the tray icon and therefore exists even when Windows starts OmniDeck in the tray (`--minimized`). Last come the governor and the daily update check. Shutdown is explicit: closing the window hides it in the tray unless the user turned that off, and otherwise only Quit ends the process. Notifications are tray balloons behind `IOdNotifier`, which Windows 11 shows as toasts, so the app needs no Windows-specific target framework or WinRT projection. The tray icon's image, a bold "O" on the accent color, is drawn in code (`OdTrayIcon`) and loaded from ICO bytes so that it owns its handle. H.NotifyIcon's `GeneratedIconSource` is avoided: in 2.4.1 it leaks an icon handle on every redraw and adds a redraw handler on every brush change, which used up the process's GDI objects after a few accent changes (fixed in 0.1.1). The Velopack update service lives in the app, because Core must not reference Velopack.

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

`IOdModuleContext` gives modules their own settings document (one JSON row per module in the `module_settings` table), the notifier (tray balloons, which Windows 11 shows as toasts; clicking one opens the module that raised it), a logger, the clock, the shared HTTP client and the database for the module's own tables. The HTTP client (`OdHttp`) is one `HttpClient` for OmniDeck's own features: it names OmniDeck in its User-Agent, keeps no cookies and gives up after 15 seconds. Module tables are migrated with Core's migrations, so one schema version covers the database. Modules never construct these themselves. Quitting closes every open module first (`OdModuleHost.CloseAllAsync`), so each one can save its state and let go of what it holds.

`CreateView()` returns a WPF `FrameworkElement`, so wherever `IOdModule` lives must reference WPF. P1 decided to let `OmniDeck.Core` take that dependency (`UseWPF`) rather than add an abstractions project. Core holds the contract, `OdModuleHost` and the localization layer (`OdLocalization`, `{res:OdLoc Key}`), so modules depend on Core alone; Core still never references the app, Velopack or WPF-UI (an architecture test checks it), which is why `OdModuleInfo.Icon` is a `SymbolRegular` name rather than a symbol.

`OdModuleHost` runs the contract. It initializes a module on first open, builds its view when it has none (first open, or after hibernation), keeps the view of the visible module, and moves hidden ones down the ladder when the shell or the governor asks; it refuses to suspend or hibernate a module whose `CanSuspendAsync` returns false. Calls run one at a time on the UI thread. The shell's navigation view is chrome only: every module item navigates to one shared stage page, whose content is the view the host hands back, so the control never creates or caches module views.

Native modules run in-process. True RAM release for them means honoring `SuspendAsync`/`HibernateAsync`: dispose charts, bitmaps, timers, caches; the governor follows up with a GC. Web modules get real process-level release for free because WebView2 is out-of-process by design.

## 3. Web runner internals

As built in P2 (`OmniDeck.Web`):

- **Environment** (`OdWebRuntime`): one `CoreWebView2Environment` for the whole app, so the browser and GPU processes are shared across web apps. It is created when the first web app opens, with its user data folder in the data root's `WebView2\` folder. The runtime counts open browsers: after the last one closes, the shared browser process exits and writes its cookies out, and a web app opened meanwhile waits for that exit (up to 3 s), because a controller created during it stalls for seconds (docs/spikes/03-memory.md). Quitting closes every module, then waits the same way before the process ends. Without an installed WebView2 Runtime, a web app shows a notice whose button opens Microsoft's download page.
- **Profiles:** one WebView2 profile per catalog `profile` value (`CoreWebView2ControllerOptions.ProfileName`). `google` is shared (one sign-in for Gmail/Calendar/Drive/Maps/YouTube); every other app is isolated. Cookies live in the profile, which is what lets **hibernate = full dispose** without losing sessions.
- **Modules:** each catalog entry is an `OdWebAppModule` (Heavy, titled with the brand name, which is never translated). Its view (`OdWebAppView`) hosts the WPF `WebView2` control; notices sit above the browser rather than over it, because no WPF element can cover the browser's native window.
- **Per-app pipeline on creation:** apply settings (`AreDevToolsEnabled=false` in Release, `IsPasswordAutosaveEnabled=false`, `IsGeneralAutofillEnabled=false`) → attach `NavigationStarting` → `NewWindowRequested` → `ProcessFailed` → `NotificationReceived` → `PermissionRequested` → start the CDP console capture → navigate.
- **Navigation policy** (`OdNavPolicy`, a pure function of the entry and the catalog): a main-frame navigation stays in the app when it is HTTPS to a host that `navAllow` or one of the entry's `navAllowSets` covers (whole-label suffix match, case-insensitive), or an `about:`, `data:` or `blob:` page. Other web and `mailto:` links open in the default browser; every other scheme is blocked and only the scheme is logged. Frames inside a page are not filtered. A popup (`NewWindowRequested`) to a `popupAllow` host opens in an in-app window (`OdWebPopupWindow`) on the same profile, so a sign-in popup lands in the app's cookies; any other popup goes to the default browser.
- **Link loop guard** (`OdExternalLinks`): the same link (scheme, host and path) opens in the browser at most 3 times in 30 s; after that it is held back and the app shows a notice, once per 30 s. In P0, Google retried a blocked sign-in hop 15 times in 40 seconds (docs/spikes/02-google-sign-in.md).
- **Suspend path:** set `MemoryUsageTargetLevel = Low` → `TrySuspendAsync()` (WebView2 refuses while the control is visible; that is logged at Debug). **Resume:** set target `Normal`, and `Resume()` if still suspended.
- **Hibernate path:** the module keeps the page on screen in memory, then disposes the control, which releases the app's processes. Reopening creates a new control on the same profile and navigates to that page. Closing the module forgets the page, and so does a restart: the next session opens `startUrl`.
- **Crash recovery:** `ProcessFailed` for the page or the browser reloads the app once, on the page it showed; a second failure within a minute shows a notice with a Reload button instead. Frame, GPU and helper process failures recover on their own and are only logged.
- **Console capture:** CDP `Runtime.consoleAPICalled`, `Runtime.exceptionThrown` and `Log.entryAdded` feed two rings kept by the module (`OdConsoleLog`: the last 200 entries and the last 50 errors), so they survive hibernation. They are formatted like DevTools (`%s`, `%d`, `%c`), held only for the session, and are there for the bug report.
- **Notifications:** `NotificationReceived` is always marked handled, so WebView2 never shows its own notification window.
  - When the app's notifications are on, the notification becomes a tray notification: "‹App›: ‹title›" with the body below, cut to what a balloon holds. That switch is in Settings → Web apps and defaults to the catalog's `notifications`.
  - Then `ReportShown` runs. Clicking the notification opens the app and calls `ReportClicked`, so the page can open the conversation.
  - The Notifications permission follows the same switch, without asking.
  - An app with notifications on is resident: the governor never suspends it for being hidden (RAM-GOVERNOR.md §3).
  - Only non-persistent notifications (`new Notification(...)` from a page) reach WebView2's event. Service-worker push does not.
- **Permissions** (`OdWebPermissions`, `OdWebPreferences`):
  - Kinds that reach the user's devices or data are asked about once per app: camera, microphone, location, clipboard reading, motion sensors, several downloads at once, file access, local fonts, MIDI system-exclusive messages and window placement.
  - The question shows in a bar above the browser, because a content dialog cannot cover the browser's native window. The page waits on a deferral until the user answers.
  - The answer is kept in `settings.json` (`web.apps.<id>.permissions`). `SavesInProfile` is false, so the profile keeps no answers of its own.
  - Autoplay and persistent storage are left to the browser. Any other kind is refused.
  - Settings → Web apps lists each app's answers; "Ask again" forgets them.
- **Downloads:** WebView2's own download flyout. It saves to the user's Downloads folder and handles SmartScreen verdicts and dangerous-file prompts, which a download row of OmniDeck's own would have to repeat.
- **Clearing data:** Settings → Web apps → Clear data, per profile group:
  - It closes the group's apps, then clears the profile's browsing data (`ClearBrowsingDataAsync`) through a hidden browser on that profile, hosted by the main window.
  - It also tells sign-in health that an empty profile is expected.
- **Sign-in health** (`OdSignInHealth`): when an app closes or hibernates, and at the first load of each profile in a session, it counts the cookies the profile holds for the app's start page (the number only, never a name or value), keeps the count in `app_meta` (`web.cookies.<profile>`) and logs it. A profile that had cookies at the last close and comes back with none is logged as a warning. Starting OmniDeck with `--wv2-logging` also turns on Chromium's own logging (`--enable-logging --v=0`) in the WebView2 folder, for chasing a lost sign-in while dogfooding.
- **Catalog refresh** (`OdCatalogService`, as built in P2): each build carries the catalog as of its commit (`catalog\webapps.json` next to the executable), and the session uses the newer of that copy and the last one fetched (`%LocalAppData%\OmniDeck.Data\catalog\webapps.json`), by `updated`. A minute after startup, and then at most once a day, it fetches `main`'s copy from the repository's raw URL over HTTPS with `If-None-Match`. It parses strictly (unknown or missing properties are errors) and applies the same rules as `validate-catalog.ps1` (`OdCatalogValidator`, tested against the repository's file). A valid catalog replaces the last fetched one atomically and takes effect at the next start, so the modules on screen never change mid-session; anything else is logged and dropped. Requests are never retried before the next daily check.

## 4. RAM governor (summary — full spec in RAM-GOVERNOR.md)

A single scheduler owns all state transitions. Inputs: per-module visibility + last-interaction time, pin status (`CanSuspendAsync` / `IsDocumentPlayingAudio`), and total footprint = private commit (`Process.PrivateMemorySize64`) of the shell plus the WebView2 processes, which the web runner reports per app (`IOdMemoryReporter`, renderers matched to apps through `GetProcessExtendedInfosAsync`) with the browser, GPU and utility processes as one shared figure. Policies: idle timers demote Active→Idle→Suspended→Hibernated; soft cap demotes LRU modules early; hard cap force-hibernates and toasts. The task-manager panel is a read/write view over the same scheduler (manual suspend/kill). In code: `OdRamGovernor` in `OmniDeck.Core/Governor`, polling every 10 s on the UI thread through `OdModuleHost`, behind `IOdMemory` so tests drive it with a fake gauge and `FakeTimeProvider`.

## 5. Storage

All app data lives under the **data root** `%LocalAppData%\OmniDeck.Data`:

- `omnideck.db` — SQLite via Dapper. Tables per module (`calendar_events`, `stocks_watchlist`, `stocks_quotes_cache`, `weather_cache`, `media_playlists`, …) plus `app_meta` (legal acknowledgment, schema version). Migrations = ordered idempotent SQL scripts executed at startup.
- `settings.json` — typed settings tree; import/export from the Data settings page.
- `WebView2\` — the WebView2 user data folder (profiles, cookies, caches).
- `logs\`, `reports\` — Serilog rolling files and bug-report zips.
- Secrets: only the optional Twelve Data key, encrypted with DPAPI (`ProtectedData`, CurrentUser scope).

Why not `%LocalAppData%\OmniDeck`: that is Velopack's install folder (packId `OmniDeck`). Velopack deletes it entirely on uninstall and when Setup is run again over an existing install, so nothing the user owns may live there. The data root is a sibling folder that survives updates and reinstalls; the Velopack uninstall hook (`VelopackApp.Build().OnBeforeUninstallFastCallback`, P1) deletes it on uninstall, as PRIVACY promises. It is local, not roaming, because WebView2 caches can reach several GiB.

## 6. Logging & failure handling

Serilog rolling files, kept for 7 days by age (a burst of errors rolls a new file every 10 MB, so a file count alone could push out the week before it). Global handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) log, show a friendly dialog, and offer the bug-report flow. A failure that repeats is reported once a minute, with the number of repeats in between, so one fault in a loop can neither stack dialogs nor flood the logs. When a web app's page or browser process fails, the app reloads once by itself; a second failure within a minute shows a notice with a Reload button (§3).

**Bug reports**, as built in P2. Two buttons open the same dialog: About → Report a bug, and Report bug on the error dialog, which also passes in the error it showed.

- **What the report holds:**
  - OmniDeck's version and commit, Windows' version and build, and the UI preset;
  - each module's state, how long it has been hidden, and the memory it uses;
  - the last 500 lines of the logs;
  - the sign-in cookie count of each profile;
  - for a web app, its console capture.
- **Scrubbing** (`OdScrubber` in `OmniDeck.Core/Reports`, a pure function) runs over every file before it is written. It replaces the user's profile folder with `%USERPROFILE%`, cuts URLs back to scheme, host and path, and removes:
  - cookie values;
  - bearer tokens, JWTs, and long hex or base64 runs;
  - email addresses;
  - numeric ids of 15 digits or more.
- **Preview:** the dialog shows the issue text the report will carry. Picking another subject composes the report again.
- **On Save:**
  - `OdBugReports` writes `reports\bugreport-‹stamp›.zip` (issue.md, system.json, logs.txt and console-‹app›.txt for a web app) and keeps the ten newest.
  - File Explorer opens with the zip selected.
  - The browser opens the bug form with only the short fields (version, Windows, preset, module) in its query.
- Nothing is uploaded (PRIVACY §3).

A content dialog cannot cover a web app's browser, which is a native window. So while any dialog is open, the main window hides the module stage, and with it the browser.

## 7. Updates & packaging

Velopack, per-user, GitHub Releases channel, delta updates; update check on About open and once per day in the background (notify-only, never auto-restart). Builds below 1.0 are GitHub pre-releases, so their update check includes pre-releases. In code: `OdVelopackUpdateService` behind `IOdUpdateService`; Settings → About checks when opened and offers *Download and restart*, and `OdUpdateWatcher` checks a few minutes after start, then daily, and notifies once per new version. A development build is not installed, so it does neither.

Releases are built by [`release.yml`](../.github/workflows/release.yml), self-contained in this repository: a GPG-signed `vX.Y.Z[-suffix]` tag on `main` runs `scripts/pack.ps1` (self-contained `win-x64` publish + `vpk pack`), build-provenance attestations, a draft GitHub Release with an SPDX SBOM and SHA-256 checksums, release notes from CHANGELOG, publication, and a Discord announcement — see [RELEASING](../RELEASING.md). The WebView2 Runtime ships with Windows 11; if it is ever missing, Velopack's `--framework webview2` bootstrapper can install it (evaluate in P2).
