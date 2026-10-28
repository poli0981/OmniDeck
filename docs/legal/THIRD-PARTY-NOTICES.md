# OmniDeck — Third-Party Notices

OmniDeck is licensed under GPL-3.0. This file lists the third-party components OmniDeck builds include, with their licenses, and ships in the app's `legal` folder. The self-contained .NET runtime's own license and third-party notices ship next to it, in `legal\Microsoft.NETCore.App\` and `legal\Microsoft.WindowsDesktop.App\`.

**Maintenance note:** verify this file against the shipped packages at every release (`dotnet list src/OmniDeck.App package --include-transitive` plus a license check) — it is authoritative only when it matches what ships. When a planned component (§2) lands, move it to §1 and add its license text to §6.

## 1. Bundled in current builds

| Component | Version | License | Role |
|---|---|---|---|
| .NET runtime (`Microsoft.NETCore.App`) | 10.0.x | MIT | runtime, self-contained; includes third-party code listed in its own `THIRD-PARTY-NOTICES.TXT` |
| Windows Desktop runtime / WPF (`Microsoft.WindowsDesktop.App`) | 10.0.x | MIT | UI framework |
| WPF-UI + WPF-UI.Abstractions (lepoco) | 4.3.0 | MIT | Fluent controls and theme |
| Fluent UI System Icons (inside WPF-UI) | — | MIT | icon font |
| Velopack | 1.2.161 | MIT | installer & updates |

## 2. Planned components (bundled from the version that introduces them)

| Component | License | Role | Phase |
|---|---|---|---|
| CommunityToolkit.Mvvm | MIT | MVVM infrastructure | P1 |
| Microsoft.Extensions.DependencyInjection | MIT | dependency injection | P1 |
| Serilog + Serilog.Sinks.File | Apache-2.0 | logging | P1 |
| Dapper | Apache-2.0 | data access | P1 |
| Microsoft.Data.Sqlite / SQLitePCLRaw / e_sqlite3 (SQLite) | MIT / Apache-2.0 / public domain | local database | P1 |
| H.NotifyIcon.Wpf | MIT | tray icon | P1 |
| Microsoft.Web.WebView2 (SDK) | BSD-3-Clause (Microsoft) | web app engine bindings | P2 |
| Ical.Net (+ NodaTime) | MIT (+ Apache-2.0) | iCalendar import/export | P2 |
| LibVLCSharp.WPF | LGPL-2.1-or-later | media bindings | P2 |
| libVLC (`VideoLAN.LibVLC.Windows`) | LGPL-2.1-or-later core; some plugins GPL-2.0-or-later | media engine | P2 |
| ScottPlot.WPF (+ SkiaSharp, OpenTK) | MIT | stock charts | P3 |

libVLC note: once the media player ships, OmniDeck redistributes libVLC binaries unmodified. Source for the exact libVLC version shipped is available from VideoLAN; the combination is distributed under GPL-3.0 terms, compatible with the LGPL-2.1+ and GPL-2.0+ components.

## 3. Not bundled

- **Microsoft Edge WebView2 Runtime** — ships with Windows 11 and is updated by Microsoft under Microsoft's license terms; OmniDeck does not redistribute it.
- **Development-time tools** (used to build and check OmniDeck, never distributed): Roslynator (Apache-2.0), Meziantou.Analyzer (MIT), ReferenceTrimmer (MIT), XamlStyler (Apache-2.0), xUnit (Apache-2.0), coverlet (MIT), Microsoft.NET.Test.Sdk (MIT), Velopack CLI `vpk` (MIT), SonarScanner for .NET (LGPL-3.0), SonarAnalyzer.CSharp (SONAR Source-Available License v1.0).

## 4. Data services (not bundled; accessed at runtime)

| Service | Terms | Use |
|---|---|---|
| Open-Meteo | open API; attribution appreciated, non-commercial free tier | weather & geocoding — "Weather data by Open-Meteo.com" shown in the module footer |
| Stooq | free quotes per stooq.com terms | default EOD stock data |
| Twelve Data | user's own API key under Twelve Data's terms | optional intraday stock data |
| GitHub | GitHub Terms of Service | releases, catalog refresh, issues |

## 5. Third-party web applications

Web apps in the catalog (Spotify, Discord, YouTube, Gmail, Google Calendar, Google Drive, Google Maps, Facebook, Messenger) are loaded from their providers' own servers and are not components of OmniDeck. All associated trademarks belong to their respective owners; see DISCLAIMER.md.

## 6. License texts

### MIT License

Applies to the components in §1, with these copyright notices:

- .NET runtime and Windows Desktop runtime (WPF): Copyright (c) .NET Foundation and Contributors
- WPF-UI and WPF-UI.Abstractions: Copyright (C) 2021-2026 Leszek Pomianowski and WPF UI Contributors
- Fluent UI System Icons: Copyright (c) 2020 Microsoft Corporation
- Velopack: Copyright © 2021 Caelan Sayler; Copyright © 2024 Velopack Ltd.

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The .NET runtime's full license and third-party notices: `legal\Microsoft.NETCore.App\LICENSE.TXT`, `legal\Microsoft.NETCore.App\THIRD-PARTY-NOTICES.TXT`, and `legal\Microsoft.WindowsDesktop.App\LICENSE` in the app folder.

## Contact

Licensing or attribution concerns: copyright@poli0981.dev.
