# OmniDeck — Third-Party Notices

OmniDeck is licensed under GPL-3.0 and includes or depends on the third-party components below. Full license texts are available at the linked projects; a `licenses/` folder with copies ships with each release. **Maintenance note:** verify names/licenses against the lockfile at each release (`dotnet list package` + license audit) — this table is authoritative only when it matches the shipped packages.

## Bundled / referenced components

| Component | License | Role |
|---|---|---|
| Microsoft Edge WebView2 SDK & Runtime | Microsoft proprietary (freely distributable; runtime delivered with Windows/Edge) | web app engine |
| WPF-UI (lepoco) | MIT | Fluent theme base |
| CommunityToolkit.Mvvm | MIT | MVVM infrastructure |
| Serilog + Serilog.Sinks.File | Apache-2.0 | logging |
| Dapper | Apache-2.0 | data access |
| Microsoft.Data.Sqlite / SQLite | MIT / Public Domain | local database |
| ScottPlot 5 | MIT | stock charts |
| LibVLCSharp | LGPL-2.1-or-later | media bindings |
| libVLC (VideoLAN.LibVLC.Windows) | LGPL-2.1-or-later core; plugins GPL-2.0-or-later | media engine |
| Ical.Net | MIT | iCalendar import/export |
| H.NotifyIcon.Wpf | MIT | tray icon |
| Velopack | MIT | installer & updates |
| Roslynator, Meziantou.Analyzer, XamlStyler, ReferenceTrimmer | MIT/Apache-2.0 (dev-time only, not distributed) | code quality |

libVLC note: OmniDeck redistributes libVLC binaries unmodified. Source for the exact libVLC version shipped is available from VideoLAN; the combination is distributed under GPL-3.0 terms compatible with LGPL-2.1+ and GPL-2.0+ components.

## Data services (not bundled; accessed at runtime)

| Service | Terms | Use |
|---|---|---|
| Open-Meteo | open API; attribution appreciated, non-commercial free tier | weather & geocoding — "Weather data by Open-Meteo.com" shown in the module footer |
| Stooq | free quotes per stooq.com terms | default EOD stock data |
| Twelve Data | user's own API key under Twelve Data's terms | optional intraday stock data |
| GitHub | GitHub Terms of Service | releases, catalog refresh, issues |

## Third-party web applications

Web apps in the catalog (Spotify, Discord, YouTube, Gmail, Google Calendar, Google Drive, Google Maps, Facebook, Messenger) are loaded from their providers' own servers and are not components of OmniDeck. All associated trademarks belong to their respective owners; see DISCLAIMER.md.
