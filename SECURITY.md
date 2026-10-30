# Security Policy

## Supported versions

OmniDeck is pre-release software ([roadmap](docs/ROADMAP.md)); only the **latest release** receives fixes. The WebView2 engine itself is patched independently by Microsoft through the evergreen WebView2 Runtime that ships with Windows 11 — keep Windows and Edge updated.

## Reporting a vulnerability

Please do **not** open a public issue for a security problem. Report it privately, in either of these ways:

- **[GitHub private vulnerability reporting](https://github.com/poli0981/OmniDeck/security/advisories/new)** (Security → Advisories → Report a vulnerability) — preferred;
- email **security@poli0981.dev**.

Include the affected version (About in the app, or the release tag), steps to reproduce, and the impact you observed or expect. You can expect an initial response within 7 days. If the report concerns a third-party component (libVLC, the WebView2 Runtime, a NuGet package), we will coordinate or redirect upstream as appropriate.

## Hardening summary (what the app does)

These are design requirements from the [SPEC](docs/SPEC.md); each one is enforced as its feature lands.

- Per-user install, **no admin elevation anywhere** in the app.
- Web apps run only from the reviewed catalog: per-app navigation allowlists, per-app cookie profiles, SSO popups restricted to listed identity domains, everything else opens in your default browser.
- Release builds disable the WebView2 DevTools UI and browser password autosave/autofill; OmniDeck never sees or stores web credentials.
- The only secret the app stores (optional Twelve Data API key) is encrypted with Windows DPAPI, current-user scope.
- All service calls (Open-Meteo, Twelve Data, GitHub) are HTTPS-only.
- Bug reports scrub query strings, cookie values, and token-shaped strings before packaging; nothing is ever uploaded automatically — you attach the zip yourself.
- No telemetry, no analytics, no crash auto-upload (see [PRIVACY](docs/legal/PRIVACY.md)).

## Supply chain and repository

- **Dependencies:** Dependabot (NuGet, .NET SDK, GitHub Actions). NuGet Audit fails the restore on moderate-or-worse advisories, transitive packages included; Dependency Review blocks pull requests that add vulnerable dependencies or licenses incompatible with GPL-3.0.
- **Analysis:** CodeQL (C# and the workflows themselves) on every push and pull request, plus weekly; SonarCloud quality gate; OpenSSF Scorecard.
- **CI:** every GitHub Action is pinned to a full commit SHA; workflow tokens are read-only by default.
- **`main`** accepts changes only through pull requests with passing required checks and verified (signed) commits; force-pushes and deletion are blocked. Release tags (`v*`) are GPG-signed, and the release workflow refuses unsigned tags and tags that are not on `main`. Published tags cannot be moved or deleted.
- **Releases** carry build-provenance attestations, an SPDX SBOM (`sbom.spdx.json`), and SHA-256 checksums (`checksums.txt`). Verify a download with:

  ```bash
  gh attestation verify OmniDeck-win-Setup.exe --repo poli0981/OmniDeck
  ```
- **Catalog:** installed apps fetch `catalog/webapps.json` from `main`, which is why `main` is protected as above; the app validates the fetched file and keeps its bundled copy on any failure.

## Code signing

Releases are not yet Authenticode-signed, so Windows SmartScreen may warn about an unknown publisher. Verify downloads with the attestation and checksums above.
