# Changelog

All notable changes to OmniDeck are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- One OmniDeck per Windows session: starting it again brings the running window to the front.
- Settings stored in `%LocalAppData%\OmniDeck.Data\settings.json`, written atomically. A damaged
  file is set aside and defaults take over.
- Local log files in `%LocalAppData%\OmniDeck.Data\logs`, kept for seven days, plus crash handling
  that shows a support code and keeps OmniDeck running.
- Uninstalling removes `%LocalAppData%\OmniDeck.Data`, as the privacy policy promises.
- A local database, `%LocalAppData%\OmniDeck.Data\omnideck.db`, with a versioned schema that
  upgrades itself at startup.

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

[Unreleased]: https://github.com/poli0981/OmniDeck/compare/v0.0.1-alpha.1...HEAD
[0.0.1-alpha.1]: https://github.com/poli0981/OmniDeck/releases/tag/v0.0.1-alpha.1
