# OmniDeck — Architecture

Everything is a **module** with a governed lifecycle. The shell is a thin host: navigation, theme engine, settings, legal gate, and the two subsystems that make the concept work — the **web runner** and the **RAM governor**.

## 1. Solution layout

```
OmniDeck.slnx
src/
  OmniDeck.App/        WPF shell: windows, navigation, tray, command palette,
                       theme engine, legal gate, settings UI, task-manager panel
  OmniDeck.Core/       contracts (IOdModule, IOdQuoteProvider, ...), RAM governor,
                       storage (SQLite/Dapper, settings), toast service,
                       bug-report composer, update service (Velopack), i18n helpers
  OmniDeck.Modules/    built-in mini-apps: Clock, Timer, Calculator, Weather,
                       Calendar, Stocks, Media — one folder per module
  OmniDeck.Web/        web runner: environment/profile management, catalog loader,
                       navigation policy, notifications bridge, permissions,
                       downloads, CDP console capture
tests/
  OmniDeck.Tests/      xUnit; governor state machine, providers, catalog validation,
                       scrubber, rate limiter
catalog/webapps.json   curated web app catalog (also fetched from raw.githubusercontent)
```

Naming: namespace `OmniDeck.*`, public type prefix `Od*` (`OdModuleHost`, `OdRamGovernor`, `OdWebRunner`, `OdCatalogEntry`).

Patterns: MVVM via CommunityToolkit.Mvvm; constructor DI via `Microsoft.Extensions.DependencyInjection`; no service locator.

## 2. Module contract

```csharp
public enum OdRamClass { Light, Heavy }          // governor priority hint
public enum OdModuleState { Created, Active, Idle, Suspended, Hibernated, Closed }

public interface IOdModule
{
    OdModuleInfo Info { get; }                   // Id, Name, Icon, Category, RamClass
    FrameworkElement CreateView();               // called on Activate after hibernation
    Task InitializeAsync(IOdModuleContext ctx);  // once per process lifetime
    Task ActivateAsync();                        // became visible
    Task<bool> CanSuspendAsync();                // e.g. media playing => false (pinned)
    Task SuspendAsync(OdSuspendReason reason);   // hidden, or governor pressure
    Task HibernateAsync();                       // release *everything*; keep tiny state blob
    Task CloseAsync();                           // user closed the module
}
```

`IOdModuleContext` gives modules storage (scoped SQLite access + settings section), the toast service, logger, and the HTTP client factory — modules never construct these themselves.

Native modules run in-process. True RAM release for them means honoring `SuspendAsync`/`HibernateAsync`: dispose charts, bitmaps, timers, caches; the governor follows up with a GC. Web modules get real process-level release for free because WebView2 is out-of-process by design.

## 3. Web runner internals

- **Environment:** one `CoreWebView2Environment` for the whole app → browser/GPU processes are shared across web apps. Created lazily on first web app open.
- **Profiles:** `CoreWebView2Profile` per catalog `profile` value. `google` is shared (one sign-in for Gmail/Calendar/Drive/Maps/YouTube); every other app is isolated. Cookies live in the profile, which is what lets **hibernate = full dispose** without losing sessions.
- **Per-app pipeline on creation:** apply settings (`AreDevToolsEnabled=false` in Release, `IsPasswordAutosaveEnabled=false`, `IsGeneralAutofillEnabled=false`) → attach `NavigationStarting` (navAllow suffix check) → `NewWindowRequested` (popupAllow → in-app popup on same profile; else default browser) → `PermissionRequested` → `NotificationReceived` → `DownloadStarting` → start CDP console capture (`Runtime.enable`, `Log.enable`).
- **Suspend path:** hide the control → `TrySuspendAsync()` → set `MemoryUsageTargetLevel = Low`. **Resume:** set target `Normal`, show; WebView2 resumes automatically on visibility.
- **Hibernate path:** persist `{ url }`, `Dispose()` the control and `CoreWebView2`. Reopen = new control on the same profile navigating to the saved URL.
- **Catalog refresh:** at most daily, fetch `catalog/webapps.json` from this repo's raw URL over HTTPS; validate against schema version; on any validation failure keep the bundled copy.

## 4. RAM governor (summary — full spec in RAM-GOVERNOR.md)

A single scheduler owns all state transitions. Inputs: per-module visibility + last-interaction time, pin status (`CanSuspendAsync` / `IsDocumentPlayingAudio`), and total footprint = shell `Process.WorkingSet64` + `CoreWebView2Environment.GetProcessInfos()` working sets. Policies: idle timers demote Active→Idle→Suspended→Hibernated; soft cap demotes LRU modules early; hard cap force-hibernates and toasts. The task-manager panel is a read/write view over the same scheduler (manual suspend/kill).

## 5. Storage

- `%LocalAppData%\OmniDeck\omnideck.db` — SQLite via Dapper. Tables per module (`calendar_events`, `stocks_watchlist`, `stocks_quotes_cache`, `weather_cache`, `media_playlists`, …) plus `app_meta` (legal acceptance, schema version). Migrations = ordered idempotent SQL scripts executed at startup.
- `settings.json` — typed settings tree; import/export from the Data settings page.
- Secrets: only the optional Twelve Data key, encrypted with DPAPI (`ProtectedData`, CurrentUser scope).

## 6. Logging & failure handling

Serilog rolling files (7-day retention). Global handlers (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) log, show a friendly dialog, and offer the bug-report flow. `CoreWebView2.ProcessFailed` triggers an automatic single reload of that app before surfacing an error tab.

## 7. Updates & packaging

Velopack, per-user, GitHub Releases channel, delta updates; update check on About open and once per day in the background (notify-only, never auto-restart). Release builds are produced by the `release.yml` workflow calling the shared ops-repo pipeline.
