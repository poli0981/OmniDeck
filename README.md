# OmniDeck

> One deck for your everyday apps — a Windows 11 host for native mini-apps and curated web apps.

[![Status](https://img.shields.io/badge/status-pre--alpha-red)](docs/ROADMAP.md)
[![Platform](https://img.shields.io/badge/platform-Windows%2011%20x64-blue)](#install)
[![License](https://img.shields.io/badge/license-GPL--3.0-green)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10%20LTS-purple)](https://dotnet.microsoft.com/)
[![CI](https://github.com/poli0981/OmniDeck/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/poli0981/OmniDeck/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=poli0981_OmniDeck&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=poli0981_OmniDeck)

OmniDeck combines a set of lightweight native mini-apps (weather, calculator, stocks, media player, clock, timer, calendar) with a curated runner for popular web apps (Spotify, Discord, YouTube, Gmail, Google Calendar, Google Drive, Google Maps, Facebook, Messenger) — no separate installs, one window, one tray icon.

It is built on WPF (.NET 10 LTS) and **WebView2**, which reuses the Edge runtime that ships with Windows 11 instead of bundling a browser engine. That is what keeps the installer small while still running full web apps.

**Project status: pre-alpha.** Development starts with phase P0 on 2 November 2026; the 0.9 public beta is planned for April 2027 and v1.0 for May 2027 ([roadmap](docs/ROADMAP.md)). Builds published before then are pipeline and development builds without the features below — follow along, but don't install them expecting a working app yet.

## Planned features (v1.0)

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

- **Spotify** runs the Web Player: audio quality is lower than the desktop app, and playback requires DRM (Widevine) support in the WebView2 runtime. A P0 spike decides whether the entry ships; if DRM is unavailable, it ships disabled with an explanation (see the [ROADMAP](docs/ROADMAP.md) risk register).
- **Discord** web has no native global push-to-talk and limited screen sharing. OmniDeck provides a host-side global hotkey as partial mitigation.
- Web app availability, layout, and sign-in flows are controlled by the respective service providers and can change or break at any time.

## Install

1. Requirements: **Windows 11 (24H2 or later), x64.** Windows 10 is not supported. The Microsoft Edge **WebView2 Runtime** ships with Windows 11.
2. Download a build from [Releases](https://github.com/poli0981/OmniDeck/releases) and run `OmniDeck-win-Setup.exe` (per-user, no admin required). Every version before 1.0 is published as a **pre-release** for testing.
3. On first run (from P1 on), review and acknowledge the legal documents (EULA, disclaimer, privacy policy, third-party notices), then pick a UI preset and language.

Updates are delivered in-app via Velopack; you can also check manually from **About**. Builds are not yet Authenticode-signed, so Windows SmartScreen may warn about an unknown publisher — see [SECURITY](SECURITY.md) for how to verify a download.

## Reporting bugs

Use **About → Report bug** inside the app (from P2). It pre-fills a GitHub issue with the app version, OS build, active modules, and captured console errors from the affected web app, and saves a `bugreport-*.zip` (logs) to `%LocalAppData%\OmniDeck.Data\reports` that you can attach. Sensitive values (query strings, cookies, tokens) are scrubbed before packaging. You can also file manually with the [bug template](https://github.com/poli0981/OmniDeck/issues/new?template=bug_report.yml).

## Documentation

| Doc | Contents |
|---|---|
| [SPEC](docs/SPEC.md) | Functional spec for every module and the web runner |
| [ARCHITECTURE](docs/ARCHITECTURE.md) | Solution layout, module lifecycle, WebView2 management, storage |
| [RAM-GOVERNOR](docs/RAM-GOVERNOR.md) | States, thresholds, pinning, measurement |
| [UI-MODES](docs/UI-MODES.md) | Design tokens and the three presets |
| [WPFUI-CONVENTIONS](docs/WPFUI-CONVENTIONS.md) | How the codebase uses WPF-UI: windows, controls, theming, navigation |
| [WEBAPP-CATALOG](docs/WEBAPP-CATALOG.md) | Catalog schema and how to propose new web apps |
| [ROADMAP](docs/ROADMAP.md) | Phases P0–P4 to v1.0, versioning, risk register |
| [CHANGELOG](CHANGELOG.md) | Release history |
| [CONTRIBUTING](CONTRIBUTING.md) | Dev setup, code style, quality gates |
| [CODE OF CONDUCT](.github/CODE_OF_CONDUCT.md) | Community standards |
| [RELEASING](RELEASING.md) | How releases are cut, published, and verified |
| [SECURITY](SECURITY.md) | Hardening summary, supply chain, vulnerability reporting |

## Contributing

Issues and PRs are welcome — the lowest-friction contribution is a new **web app catalog entry** (a single JSON object; see [WEBAPP-CATALOG](docs/WEBAPP-CATALOG.md)). For code contributions, read [CONTRIBUTING](CONTRIBUTING.md) first: CI enforces formatting, analyzers (warnings are errors), dead-code checks, and catalog validation.

## Community & contact

- Questions and ideas: [GitHub Discussions](https://github.com/poli0981/OmniDeck/discussions) · [Discord](https://discord.gg/u8tn3YypyX)
- Security vulnerabilities: [private advisory](https://github.com/poli0981/OmniDeck/security/advisories/new) or security@poli0981.dev — never a public issue
- Privacy: privacy@poli0981.dev · Legal: legal@poli0981.dev · Copyright and licensing: copyright@poli0981.dev
- Catalog entry or trademark removal requests: takedown@poli0981.dev
- Code, contributions, and Code of Conduct reports: code@poli0981.dev

## Legal

The in-app legal gate links to the documents in [`docs/legal/`](docs/legal/): [EULA](docs/legal/EULA.md) · [Disclaimer](docs/legal/DISCLAIMER.md) · [Privacy Policy](docs/legal/PRIVACY.md) · [Third-Party Notices](docs/legal/THIRD-PARTY-NOTICES.md).

All product names, logos, and brands of the web apps listed in the catalog are property of their respective owners; OmniDeck is not affiliated with or endorsed by any of them.

## License

[GPL-3.0](LICENSE) © poli0981 (SkullMute)
