# OmniDeck

> One deck for your everyday apps — a high-performance Windows host for native mini-apps and curated web apps.

![Status](https://img.shields.io/badge/status-beta-orange)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue)
![License](https://img.shields.io/badge/license-GPL--3.0-green)
![.NET](https://img.shields.io/badge/.NET-10%20LTS-purple)

OmniDeck combines a set of lightweight native mini-apps (weather, calculator, stocks, media player, clock, timer, calendar) with a curated runner for popular web apps (Spotify, Discord, YouTube, Gmail, Google Calendar, Google Drive, Google Maps, Facebook, Messenger) — no separate installs, one window, one tray icon.

It is built on WPF (.NET 10 LTS) and **WebView2**, which reuses the Edge runtime already shipped with Windows instead of bundling a browser engine. That is what keeps the installer small while still running full web apps.

**Project status:** beta. v1.0 is targeted for **January 2027**. Expect rough edges; bug reports are very welcome (see [Reporting bugs](#reporting-bugs)).

## Features

- **Native mini-apps** — clock & world clocks, countdown timer, calculator, weather (Open-Meteo), local calendar with `.ics` import/export, stock watchlist with candlestick charts (US/EU markets), and a full-codec media player (libVLC).
- **Curated web apps** — each runs in its own WebView2 profile with a per-app navigation allowlist. Web notifications become Windows toasts. Adding a new app is a JSON entry, not a release ([catalog docs](docs/WEBAPP-CATALOG.md)).
- **RAM governor** — inactive modules are automatically suspended and then hibernated, with a hard memory cap. A built-in task-manager panel shows per-module RAM with manual suspend/kill ([details](docs/RAM-GOVERNOR.md)).
- **Three UI presets** — Dynamic, Standard, and Comfort, implemented as design-token presets over one theme engine, with per-token overrides ([details](docs/UI-MODES.md)).
- **Privacy** — zero telemetry, zero data collection. Everything is stored locally ([privacy policy](docs/legal/PRIVACY.md)).
- **Languages** — English, Tiếng Việt, 日本語.

## The trade-off, stated up front

Running web apps means running Chromium processes. OmniDeck manages this aggressively (suspend → hibernate → dispose), but with several web apps active it can legitimately use several GiB of RAM. The governor enforces a hard cap of **6 GiB** (configurable); when the cap is hit, least-recently-used modules are hibernated automatically. If you want a 100 MB app, this is not that app — if you want *one* app instead of six Electron installs, it is.

## Known limitations of web apps

These are properties of the web versions of the services, documented here so they are not reported as OmniDeck bugs:

- **Spotify** runs the Web Player: audio quality is lower than the desktop app, and playback requires DRM support in the WebView2 runtime (validated during beta; see [ROADMAP](docs/ROADMAP.md) risk register).
- **Discord** web has no native global push-to-talk and limited screen sharing. OmniDeck provides a host-side global hotkey as partial mitigation.
- Web app availability, layout, and sign-in flows are controlled by the respective service providers and can change or break at any time.

## Install

1. Requirements: Windows 10 (1809+) or Windows 11, x64. The Microsoft Edge **WebView2 Runtime** must be present (preinstalled on Windows 11 and updated Windows 10; the installer checks and links to Microsoft's installer if missing).
2. Download the latest release from [Releases](../../releases) and run the installer (per-user, no admin required).
3. On first run, review and accept the legal gate (EULA, disclaimer, privacy policy, third-party notices), then pick a UI preset and language.

Updates are delivered in-app via Velopack; you can also check manually from **About**.

## Reporting bugs

Use **About → Report bug** inside the app. It pre-fills a GitHub issue with the app version, OS build, active modules, and captured console errors from the affected web app, and saves a `bugreport-*.zip` (logs) you can attach. Sensitive values (query strings, cookies, tokens) are scrubbed before packaging. You can also file manually with the [bug template](.github/ISSUE_TEMPLATE/bug_report.yml) — please include console errors.

## Documentation

| Doc | Contents |
|---|---|
| [SPEC](docs/SPEC.md) | Functional spec for every module and the web runner |
| [ARCHITECTURE](docs/ARCHITECTURE.md) | Solution layout, module lifecycle, WebView2 management |
| [RAM-GOVERNOR](docs/RAM-GOVERNOR.md) | States, thresholds, pinning, measurement |
| [UI-MODES](docs/UI-MODES.md) | Design tokens and the three presets |
| [WEBAPP-CATALOG](docs/WEBAPP-CATALOG.md) | Catalog schema and how to propose new web apps |
| [ROADMAP](docs/ROADMAP.md) | Phases P0–P4 to v1.0, risk register |
| [CONTRIBUTING](CONTRIBUTING.md) | Dev setup, code style, tooling |
| [SECURITY](SECURITY.md) | Hardening summary and vulnerability reporting |

## Contributing

Issues and PRs are welcome — the lowest-friction contribution is a new **web app catalog entry** (a single JSON object; see [WEBAPP-CATALOG](docs/WEBAPP-CATALOG.md)). For code contributions, read [CONTRIBUTING](CONTRIBUTING.md) first: the repo enforces analyzers, formatting, and dead-code checks in CI.

## Legal

The in-app legal gate links to the documents in [`docs/legal/`](docs/legal/): [EULA](docs/legal/EULA.md) · [Disclaimer](docs/legal/DISCLAIMER.md) · [Privacy Policy](docs/legal/PRIVACY.md) · [Third-Party Notices](docs/legal/THIRD-PARTY-NOTICES.md).

All product names, logos, and brands of the web apps listed in the catalog are property of their respective owners; OmniDeck is not affiliated with or endorsed by any of them.

## License

[GPL-3.0](LICENSE) © poli0981 (SkullMute)
