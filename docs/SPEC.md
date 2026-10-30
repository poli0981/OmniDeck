# OmniDeck — Functional Specification

Status: draft for v1.0 (target May 2027) · Applies to: Windows 11 (24H2 or later), x64 only

## 1. Goals and non-goals

**Goals**

- One window hosting many small tools ("mini-apps") plus popular web apps, replacing several standalone installs.
- Small installer and idle footprint; honest, enforced memory ceiling when busy (default hard cap 6 GiB).
- Zero telemetry. All state local.
- Three UI presets serving different audiences without forking the UI.

**Non-goals (v1)**

- No arbitrary user-added URLs — web apps come from the curated catalog only.
- No admin elevation, ever.
- No Google Calendar/Drive *sync* into native mini-apps (the web apps cover this; native sync is v2).
- No native map mini-app — Google Maps ships as a catalog web app instead.
- No Windows 10: .NET 10 no longer supports its consumer editions, and their security updates ended in October 2026.
- No mobile/macOS/Linux targets.

## 2. Shell

- **Navigation:** left sidebar listing pinned modules; full module library behind a "+" view. Sidebar density and item count vary by UI preset.
- **Command palette:** `Ctrl+K` opens a fuzzy launcher for every module, web app, and settings page.
- **Tray:** single-instance app with tray icon; closing the window minimizes to tray (configurable). Tray menu: open, quick timer, media controls when playing, quit.
- **Windowing:** modules open as tabs in the main window; any module can be popped out into its own top-level window.
- **Settings:** general (language EN/VI/JA, startup, tray behavior), appearance (preset + per-token overrides), memory (governor thresholds), per-module settings, data (export/import settings JSON, open data folder), about.
- **Legal gate:** on first run and whenever the acknowledged legal-doc version differs from the shipped version, show EULA, Disclaimer, Privacy Policy, and Third-Party Notices (shipped in the app's `legal\` folder, with links to this repo). The user acknowledges having read them; the acknowledgment (doc version + timestamp) is stored locally and the app stays on the gate until then. It is an acknowledgment, not a condition of the license: GPL-3.0 §9 does not require accepting the license to run the program.
- **About:** app version + commit hash, update check (Velopack), links (GitHub, Discussions, Discord), credits, **Report bug** button (§6).

## 3. Native mini-apps

All mini-apps implement the module contract in [ARCHITECTURE](ARCHITECTURE.md) and respect governor suspension.

### 3.1 Clock & timer
- World clock: multiple time zones, 12/24 h.
- Countdown timer and stopwatch; finished timers raise a Windows toast (works while minimized to tray).
- Alarms fire only while OmniDeck is running (tray counts as running); this limitation is stated in the UI.

### 3.2 Calculator
- Standard + scientific modes, calculation history (session-scoped), copy result.

### 3.3 Weather
- Provider: **Open-Meteo** forecast + geocoding APIs (keyless).
- Current conditions, hourly (48 h) and daily (7 d) forecast for multiple saved locations.
- Cache responses in SQLite with a 15–30 min TTL; render from cache when offline with a "stale" badge.

### 3.4 Calendar
- Local events in SQLite; month/week/agenda views; recurring events (daily/weekly/monthly/yearly).
- Import/export **iCalendar (.ics)** via Ical.Net.
- Event reminders raise toasts (same running-app caveat as alarms).

### 3.5 Stocks (US/EU)
- **Watchlist** with quote, day change, sparkline; detail view with candlestick/OHLC + volume charts (ScottPlot 5), range 1M/6M/1Y/5Y.
- **Provider** behind `IOdQuoteProvider`: `TwelveData`, with the user's own API key (free Basic plan or paid). The module asks for the key on first use; it is stored via **DPAPI**, never in plaintext or logs.
  - **Coverage depends on the user's plan.** The free plan covers US stocks, ETFs and US-listed ADRs. Home-market EU listings need Grow, and indices need Pro. Symbol search (`show_plan=true`, free) labels each listing with the plan it needs, and a plan error reads "not available on your Twelve Data plan".
  - **Stooq was dropped** in P0 because it now blocks scripted clients ([spike #7](spikes/07-providers.md)).
- Client-side **sliding-window rate limiting**: ≤ 8 credits per rolling 60 s and the plan's daily budget, counting failed calls. Watchlist refreshes use batch quotes. On limit or error, keep the last cached values and show `N/A` for missing fields; never delete cached data on network failure.
- Every stocks screen carries the data disclaimer footer ("delayed/indicative data — not investment advice"), mirrored in [DISCLAIMER](legal/DISCLAIMER.md).

### 3.6 Media player
- Engine: **LibVLCSharp + VideoLAN.LibVLC.Windows (x64)**, plugins trimmed to codecs/demux/audio/video output.
- Local files and folders, playlist (m3u load/save), subtitles (embedded + external `.srt`/`.ass`, encoding selection), audio track & device selection, speed 0.25–4×, snapshot.
- libVLC initializes lazily on first open; the player counts as **pinned** while playback is active (governor may not suspend it). Suspend stops and disposes the player, and hibernate disposes libVLC too: playback holds 280–520 MB committed.
- **Fullscreen** makes the module's own window cover the monitor. **Pop-out** hands the player to the new window by re-selecting the video track and seeking to the current time ([spike #6](spikes/06-libvlcsharp.md)).
- **Build:** x64 only (`VlcWindowsX86Enabled` and `VlcWindowsArm64Enabled` set to false), with the trimmed plugin set: about 86 MB raw, 39 MB compressed.

## 4. Web runner

- One shared `CoreWebView2Environment` (user data under `%LocalAppData%\OmniDeck.Data\WebView2`); one `CoreWebView2Profile` per catalog `profile` group (the `google` group is shared across Google apps for single sign-on; all others are isolated).
- **Navigation policy:** top-level navigation outside the entry's `navAllow` suffix list opens in the system default browser. `NewWindowRequested` to a `popupAllow` domain opens an in-app popup on the same profile (SSO); everything else goes to the default browser.
- **Notifications:** `NotificationReceived` is intercepted and re-raised as a Windows toast tagged with the app name; per-app opt-out in settings.
- **Permissions:** `PermissionRequested` (mic/camera/clipboard/geolocation) shows a native prompt; the decision is remembered per app and reviewable in settings.
- **Downloads:** routed through `DownloadStarting` to the user's Downloads folder with an in-shell progress row.
- **Media pinning:** while `IsDocumentPlayingAudio` is true, the module is pinned (see RAM-GOVERNOR).
- **Lifecycle:** hidden web apps are suspended via `TrySuspendAsync` + `MemoryUsageTargetLevel.Low`, later disposed entirely (session survives via profile cookies). Disposing the last web app ends the shared browser process. Full state machine in [RAM-GOVERNOR](RAM-GOVERNOR.md).
- **Google account domains:** Google's sign-in sets cookies on country-code domains (for example `accounts.google.com.vn`). The `google` entries need them in `navAllow`, through a catalog schema v2 field (`navAllowSets`) decided in P2 ([spike #2](spikes/02-google-sign-in.md)).
- **Catalog:** shipped in `catalog/webapps.json`, refreshed from this repo's raw URL at most once per day; schema and rules in [WEBAPP-CATALOG](WEBAPP-CATALOG.md).

## 5. Cross-cutting requirements

- **i18n:** all shell and mini-app strings in `.resx`, EN (neutral) / VI / JA.
- **Accessibility:** honor Windows text scaling (multiplies the preset font token) and High Contrast (overrides palette tokens); full keyboard navigation of the shell.
- **Storage:** everything lives under the data root `%LocalAppData%\OmniDeck.Data`: SQLite (`omnideck.db`, via Dapper) for module data; `settings.json` for configuration; Serilog rolling files under `logs\` (7-day retention). Never inside `%LocalAppData%\OmniDeck`, which is Velopack's install folder and is deleted on uninstall and reinstall ([ARCHITECTURE](ARCHITECTURE.md) §5).
- **Updates:** Velopack against GitHub Releases; per-user install; delta updates.
- **Security:** see [SECURITY](../SECURITY.md) — no admin, DevTools UI off in release builds, password autosave off, DPAPI for the only secret (Twelve Data key), HTTPS-only service calls; in CI, NuGet Audit (restore fails on moderate+ advisories, transitive packages included) and Dependency Review.

## 6. Bug reporting flow

1. A DevTools Protocol session per web app (`Runtime` + `Log`, always on, DevTools UI still disabled) subscribes to `Runtime.consoleAPICalled`, `Runtime.exceptionThrown` and `Log.entryAdded`. It keeps a 200-entry ring buffer per app, plus a 50-entry errors-only ring so chatty apps cannot evict errors ([spike #4](spikes/04-cdp-console.md)). .NET unhandled exceptions and the Serilog tail are collected shell-side.
2. **Report bug** composes: version, Windows build, UI preset, active modules + RAM snapshot, the relevant console ring buffer, and last log lines.
3. Before packaging, a scrubber strips query strings, cookie values, and bearer-token-shaped strings.
4. The app opens a pre-filled GitHub issue (bug template) and writes `bugreport-<timestamp>.zip` to `%LocalAppData%\OmniDeck.Data\reports` for the user to attach (issue-URL prefill has a length limit, so long logs travel in the zip).

## 7. Reference versions (verified 2026-10-28; Dependabot owns bumps from here)

| Component | Version | Notes |
|---|---|---|
| .NET | 10.0.14 runtime · SDK 10.0.403 | LTS until Nov 2028; SDK pinned in `global.json` |
| C# | 14 | ships with .NET 10 |
| WebView2 SDK (`Microsoft.Web.WebView2`) | 1.0.4314.35 | runtime is evergreen and ships with Windows 11 |
| WPF-UI | 4.3.0 | Fluent theme base; see [WPFUI-CONVENTIONS](WPFUI-CONVENTIONS.md) |
| CommunityToolkit.Mvvm | 8.4.2 | |
| Serilog / Serilog.Sinks.File | 4.4.0 / 7.0.0 | |
| Dapper / Microsoft.Data.Sqlite | 2.1.89 / 10.0.14 | |
| ScottPlot.WPF | 5.1.59 | stocks charts |
| LibVLCSharp.WPF / VideoLAN.LibVLC.Windows | 3.10.2 / 3.0.24 | media |
| Ical.Net | 5.2.3 | calendar (v5 API) |
| H.NotifyIcon.Wpf | 2.4.1 | tray |
| Velopack (+ `vpk` CLI) | 1.2.161 | installer & updates; library and CLI move together |

Dependabot (NuGet, .NET SDK, GitHub Actions) proposes updates weekly. To check by hand: `dotnet package list --outdated` and `dotnet package list --vulnerable --include-transitive`. Package versions live in each `.csproj` (no Central Package Management: GitHub's dependency graph ignores `Directory.Packages.props`); build-time analyzers live in `Directory.Build.props`.
