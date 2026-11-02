# Changelog

All notable changes to OmniDeck are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Weather: forecasts from Open-Meteo for the places you save, found by searching for a city. It
  shows the conditions now, the next 48 hours and the next 7 days, in the units your region uses or
  the ones you pick. A forecast is fetched again at most every half hour, and only while Weather is
  on screen; without a connection it shows the last forecast and says how old it is.
- Calendar: your own events in a month grid, a week list or a 30-day agenda, with repeats (daily,
  weekly, monthly or yearly, every so many, until a date) and reminders, which appear while OmniDeck
  is running. Import and export .ics files; repeats written by other calendar apps are kept as they
  are. One date of a series can be deleted without touching the rest.

### Changed
- Clicking a notification opens the module that raised it: a finished timer opens Timer.
- Quitting closes every open module first, so each one can save its state before OmniDeck exits.
- OmniDeck re-styles its windows only when Windows' dark or light mode, accent color, High Contrast or
  animation setting changes, not on every settings broadcast from Windows.

## [0.1.1] — 2026-11-02

A fix release for 0.1.0. Picking accent colors, or Windows changing a theme setting, could make
OmniDeck run out of Windows graphics handles, show "A generic error occurred in GDI+" over and over,
and stop drawing its window. Installed copies find this update in Settings → About, and say so in a
notification within a day.

### Fixed
- Changing the accent color no longer ends in "Something went wrong… A generic error occurred in
  GDI+". The notification-area icon leaked a Windows graphics handle every time it was redrawn in a
  new color, and each change redrew it more often than the last, so after enough color or theme
  changes OmniDeck ran out of handles and could stop drawing its window. The icon is now drawn by
  OmniDeck itself and redrawn only when the color actually changes.
- Toggle switches take a newly picked accent color at once. Before, they kept the previous color
  until the next change.
- An error that keeps repeating is now reported once a minute, with a count of the repeats, instead
  of every time, so a single fault can no longer stack error dialogs or fill the logs.
- Logs are kept for seven days, as the privacy policy says. Before, a burst of errors could push out
  the whole week of logs before it.

## [0.1.0] — 2026-10-30

The first installable OmniDeck, closing phase P1. It brings the shell with its sidebar, module
library, command palette and notification-area icon, plus the RAM governor and its panel. It also
has the clock, timer and calculator, the settings, legal documents and onboarding, and updates from
inside the app. The 0.1.0-alpha.1 and 0.1.0-alpha.2 notes list all of it in detail. Web apps,
weather, calendar, stocks and media arrive in P2 and P3.

### Added
- Keyboard shortcuts: Ctrl+1 to Ctrl+9 open the modules in the sidebar, in order, and Ctrl+, opens
  Settings; Ctrl+K still opens the command palette.
- Installed copies check for updates a few minutes after starting and then once a day, and say so in
  a notification when a new version is out. Opening Settings → About checks too. Nothing downloads
  until you choose to.

### Changed
- The sidebar follows the layout preset as you switch: Dynamic shows icons only, with each name in a
  tooltip, and Comfort shows at most six modules, always including the one on screen.

### Fixed
- The command palette shrinks to fit a short list of results.
- With High Contrast on, Windows' colors win over a custom accent color.
- Lines and borders in the legal documents follow the theme, High Contrast included.
- Screen readers announce the layout presets and settings choices by their names.

## [0.1.0-alpha.2] — 2026-10-30

Second alpha: the settings pages, the legal gate and onboarding. It is also the first build that can
update itself. 0.1.0-alpha.1 cannot, so install this one over it with its `Setup.exe` and choose to
overwrite; your settings and data stay. From here on, Settings → About → Check for updates finds new
releases.

### Added
- First run: the license agreement, disclaimer, privacy policy and third-party notices, shown from the
  app's `legal` folder, to acknowledge before OmniDeck opens (again after each legal update). Then a
  short setup picks the language and the layout preset.
- Settings: General (language, keep running in the notification area, start with Windows), Appearance
  (theme, layout preset, text size, animations, accent color), Memory (the governor's thresholds and
  the running modules), Data (open the data folder, export and import settings) and About (version,
  update check, links, the legal documents).
- Starting with Windows opens OmniDeck straight to the notification area.

### Changed
- Running modules moved from the sidebar into Settings → Memory; the command palette opens it too.

## [0.1.0-alpha.1] — 2026-10-30

First alpha, for trying OmniDeck out: the shell, the RAM governor and three built-in modules. Web
apps, weather, calendar, stocks and media arrive in P2–P3, and the settings pages, legal gate and
onboarding in the next alpha.

### Added
- One OmniDeck per Windows session: starting it again brings the running window to the front.
- Settings stored in `%LocalAppData%\OmniDeck.Data\settings.json`, written atomically. A damaged
  file is set aside and defaults take over.
- Local log files in `%LocalAppData%\OmniDeck.Data\logs`, kept for seven days, plus crash handling
  that shows a support code and keeps OmniDeck running.
- Uninstalling removes `%LocalAppData%\OmniDeck.Data`, as the privacy policy promises.
- A local database, `%LocalAppData%\OmniDeck.Data\omnideck.db`, with a versioned schema that
  upgrades itself at startup.
- The window follows Windows: light or dark mode, the accent color, High Contrast and the text size.
- The shell: a sidebar with pinned and open modules, a module library to open and pin them from,
  and a command palette (Ctrl+K, or the search box in the title bar) that finds modules and commands.
- Closing the window keeps OmniDeck running in the notification area; its icon's menu opens or quits
  it. Modules can raise Windows notifications from there.
- The RAM governor: modules hidden for 5 minutes are suspended and after 30 minutes hibernated.
  Above a soft memory limit the least recently used one is hibernated; above a hard limit as many as
  it takes, with a notification. The limits default to min(4.5 GB, 35 % of RAM) and min(6 GB, 50 %).
- Running modules (the sidebar's Memory item): every module's state and last use, memory in use
  against the limits, and Suspend, Hibernate, Close and Keep running controls.
- Clock: local time and any number of world clocks, in 12- or 24-hour time, with each zone's offset
  from here. The zones are remembered.
- Timer: countdowns with presets (1, 5, 10 and 25 minutes) or a custom length and name, and a
  stopwatch with laps. A finished countdown raises a Windows notification, also from the
  notification area, and the tray menu starts a 5-minute timer. Timers run only while OmniDeck
  runs, as the module says.
- Calculator: standard and scientific keypads (degrees or radians), keyboard input, the session's
  history, and copying the result.

### Changed
- Stocks: Twelve Data with the user's own API key is now the only provider. Stooq was dropped because
  its download endpoints now block scripted clients (P0 spike #7). The free Twelve Data plan covers US
  listings; home-market EU listings and indices need a paid plan.
- Legal documents (EULA, disclaimer, privacy policy) are now version 1.1, and the third-party notices
  follow: Stooq and Messenger are no longer mentioned.

### Removed
- Messenger from the web app catalog: Meta closed messenger.com on 15 April 2026 and sends web
  users to `facebook.com/messages`, which the Facebook entry already covers.

## [0.0.1-alpha.1] — 2026-10-28

Kickoff build. It proves the build, packaging, and release pipeline end to end and has no
user-facing features yet: the window only shows the version.

### Added
- Documentation suite: spec, architecture, RAM governor, UI presets, WPF-UI conventions, web app
  catalog, roadmap, and the legal set (EULA, disclaimer, privacy policy, third-party notices).
- Curated web app catalog (9 entries, schema v1) with a JSON Schema and CI validation.
- Solution skeleton: `OmniDeck.App` (Velopack bootstrap and a placeholder Fluent window),
  `OmniDeck.Core` (`OdVersionInfo`), and `OmniDeck.Tests`.
- Self-contained CI: build and tests, C# and XAML formatting, analyzers as errors, NuGet Audit,
  Velopack packaging smoke test, catalog validation, workflow and link linting, CodeQL,
  SonarCloud, Dependency Review, and OpenSSF Scorecard.
- Release pipeline: signed tags on `main` → Velopack packages, build-provenance attestations,
  SBOM, SHA-256 checksums, and a Discord announcement.
- Repository governance: protected `main` and release tags, CODEOWNERS, Dependabot, Code of
  Conduct, issue and pull request templates.

### Changed
- Roadmap re-baselined: P0 starts on 2026-11-02 and v1.0 is targeted for May 2027.
- Supported platform narrowed to Windows 11 (24H2 or later), x64.
- App data moves to `%LocalAppData%\OmniDeck.Data`, outside Velopack's install folder.

[Unreleased]: https://github.com/poli0981/OmniDeck/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/poli0981/OmniDeck/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/poli0981/OmniDeck/compare/v0.1.0-alpha.2...v0.1.0
[0.1.0-alpha.2]: https://github.com/poli0981/OmniDeck/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/poli0981/OmniDeck/compare/v0.0.1-alpha.1...v0.1.0-alpha.1
[0.0.1-alpha.1]: https://github.com/poli0981/OmniDeck/releases/tag/v0.0.1-alpha.1
