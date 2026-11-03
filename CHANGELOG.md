# Changelog

All notable changes to OmniDeck are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Weather: the next 48 hours also show as a chart, the temperature as a line over the chance of rain.
  Screen readers hear the chart sum the hours up; with the chart focused, the arrow keys read the
  hours one by one, as the mouse does when it moves over it. The list of hours stays below it.

## [0.2.0-alpha.4] — 2026-11-03

Fourth P2 alpha, the build to try out before 0.2.0: the memory governor now also rests the module
of a minimized window. The web app list every copy downloads was updated the same day: Spotify's
"Continue with Google, Facebook or Apple" buttons now sign in inside the app, and a Discord or
Facebook call that is playing sound is never put to sleep.

### Fixed
- The module on screen when you minimized OmniDeck, or closed it to the notification area, kept
  running at full size for as long as the window stayed away, even above the memory limits. It now
  rests like any other hidden module: suspended after 5 minutes and closed down after 30 (or whatever
  Settings → Memory says), unless it is playing, kept running or has notifications on. It comes back,
  on the page it showed, when the window does.
- A player that stops after playing out of sight for a while is no longer closed down at once: the
  time it spent playing does not count as time unused.

## [0.2.0-alpha.3] — 2026-11-02

Third P2 alpha, the build to try out before 0.2.0: pop-out windows, web pages in full screen, an icon
of its own, and fixes to the media player's controls.

### Added
- Any module can open in a window of its own: choose "Open in new window" in its sidebar menu, or the
  window button on its library tile. It runs there beside whatever the main window shows, and OmniDeck
  never puts it to sleep while its window is open. Closing the window puts it back in the sidebar;
  "Return to main window" shows it there. Web apps and videos keep playing through the move, and the
  module's window goes full screen like the main window.
- Web apps: a video, or anything else a page shows full screen, now fills the screen, and Esc goes
  back.
- OmniDeck has an icon of its own, a white "O" on blue: on the program, its windows, the taskbar and
  the installer.

### Changed
- Media: the Play/Pause button shows Pause while something plays.
- The roadmap is re-baselined: P2 started on 2 November 2026, as soon as P1 was done. The README,
  the spec and the architecture notes now describe what P2 built.

### Fixed
- Media: in full screen the picture started a few pixels from the left edge of the screen. It now
  fills the screen.
- Media: beside the queue, the seek bar shrank to almost nothing, and at the window's default size
  the queue button was cut off. The seek bar now has a row of its own, and the volume slider gets
  shorter before anything is cut off.

## [0.2.0-alpha.2] — 2026-11-02

Second P2 alpha: the media player and the bug report join the web apps of alpha.1. Pop-out windows
and the final polish come before 0.2.0.

### Added
- Media: play your own music and videos. Add files, a folder or an .m3u playlist, or drop them on the
  player; the queue and where you stopped are kept for next time, and the queue can be saved as an
  .m3u. OmniDeck keeps the player running while something plays, and frees its memory once it has
  been paused and out of sight for a while. Its More menu picks the audio and subtitle tracks, adds
  a subtitle file and sets its encoding, plays from a quarter to four times the speed, sends the
  sound to another device, and saves a snapshot of the picture in Pictures\OmniDeck. A double-click
  on the video, or its full-screen button, fills the screen, and Esc goes back. While something is
  loaded, the tray menu names it and offers play or pause, next and previous.
- Report a bug from Settings → About, or from the dialog that shows an error. OmniDeck shows the
  issue text first, then saves a report on this PC: the issue text, the version and Windows build,
  the modules and the memory they use, the end of the log and, for a web app, its console. It then
  opens a new GitHub issue in your browser with the short fields filled in. Nothing is sent: attach
  the report yourself. Addresses keep no query, and tokens, cookies, email addresses, long ids and
  your user name are taken out first. The ten newest reports are kept.

### Fixed
- A dialog that opened while a web app was on screen could hide behind the page. The page now steps
  aside while a dialog is open.

## [0.2.0-alpha.1] — 2026-11-02

First P2 alpha, for trying the web apps out. The eight apps of the catalog run inside OmniDeck on
Microsoft Edge WebView2, with their notifications, site permissions and memory figures, and Weather
and Calendar join the built-in modules. The bug report, Media, pop-out windows and the final polish
arrive in the next alphas.

### Added
- Weather: forecasts from Open-Meteo for the places you save, found by searching for a city. It
  shows the conditions now, the next 48 hours and the next 7 days, in the units your region uses or
  the ones you pick. A forecast is fetched again at most every half hour, and only while Weather is
  on screen; without a connection it shows the last forecast and says how old it is.
- Calendar: your own events in a month grid, a week list or a 30-day agenda, with repeats (daily,
  weekly, monthly or yearly, every so many, until a date) and reminders, which appear while OmniDeck
  is running. Import and export .ics files; repeats written by other calendar apps are kept as they
  are. One date of a series can be deleted without touching the rest.
- OmniDeck keeps its list of web apps up to date: once a day it checks the list in this repository and,
  when there is a newer valid one, uses it from the next start.
- Web apps: Spotify, Discord, YouTube, Gmail, Google Calendar, Google Drive, Google Maps and Facebook
  open inside OmniDeck like the built-in modules. The Google apps share one sign-in; every other app
  keeps its own. A link to another site opens in your default browser, while sign-in popups stay in
  the app. A web app that sleeps or is closed down to save memory comes back on the page it showed;
  one whose page crashes reloads by itself. Downloads go to your Downloads folder.
- Web app notifications appear as Windows notifications under the app's name. Clicking one opens the
  app on what it was about. Settings → Web apps turns them on or off for each app. An app with
  notifications on keeps running while hidden, so it can still notify you; OmniDeck closes it only
  when memory runs short, and tells you when it does.
- When a web app asks for your camera, microphone, location, clipboard or a similar permission,
  OmniDeck asks you above the app and remembers your answer for that app. Settings → Web apps lists
  the answers, lets an app ask again, and clears a group of apps' sign-in data.

### Changed
- Clicking a notification opens the module that raised it: a finished timer opens Timer.
- Settings → Memory shows what each open web app uses, and the browser processes they share in a row
  of their own. The memory limits count the web apps' processes too. When OmniDeck must free memory,
  it picks the app that uses more among those hidden for as long. When the shared processes alone
  keep it over the limit, OmniDeck says so instead of closing apps that would free nothing.
- Quitting closes every open module first, so each one can save its state before OmniDeck exits. With
  web apps open, it also waits a moment for the browser to save its cookies, so you stay signed in.
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

[Unreleased]: https://github.com/poli0981/OmniDeck/compare/v0.2.0-alpha.4...HEAD
[0.2.0-alpha.4]: https://github.com/poli0981/OmniDeck/compare/v0.2.0-alpha.3...v0.2.0-alpha.4
[0.2.0-alpha.3]: https://github.com/poli0981/OmniDeck/compare/v0.2.0-alpha.2...v0.2.0-alpha.3
[0.2.0-alpha.2]: https://github.com/poli0981/OmniDeck/compare/v0.2.0-alpha.1...v0.2.0-alpha.2
[0.2.0-alpha.1]: https://github.com/poli0981/OmniDeck/compare/v0.1.1...v0.2.0-alpha.1
[0.1.1]: https://github.com/poli0981/OmniDeck/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/poli0981/OmniDeck/compare/v0.1.0-alpha.2...v0.1.0
[0.1.0-alpha.2]: https://github.com/poli0981/OmniDeck/compare/v0.1.0-alpha.1...v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/poli0981/OmniDeck/compare/v0.0.1-alpha.1...v0.1.0-alpha.1
[0.0.1-alpha.1]: https://github.com/poli0981/OmniDeck/releases/tag/v0.0.1-alpha.1
