# Security Policy

## Supported versions

OmniDeck is in beta; only the **latest release** receives fixes. The WebView2 engine itself is patched independently by Microsoft through the evergreen WebView2 Runtime (delivered with Edge updates) — keep Windows/Edge updated.

## Reporting a vulnerability

Please use **GitHub → Security → Report a vulnerability** (private advisory) on this repository. Do not open public issues for security problems. You can expect an initial response within 7 days. If the report concerns a bundled third-party component (libVLC, WebView2 Runtime, a NuGet package), we will coordinate or redirect upstream as appropriate.

## Hardening summary (what the app does)

- Per-user install, **no admin elevation anywhere** in the app.
- Web apps run only from the reviewed catalog: per-app navigation allowlists, per-app cookie profiles, SSO popups restricted to listed identity domains, everything else opens in your default browser.
- Release builds disable the WebView2 DevTools UI and browser password autosave/autofill; OmniDeck never sees or stores web credentials.
- The only secret the app stores (optional Twelve Data API key) is encrypted with Windows DPAPI, current-user scope.
- All service calls (Open-Meteo, Stooq, Twelve Data, GitHub) are HTTPS-only.
- Bug reports scrub query strings, cookie values, and token-shaped strings before packaging; nothing is ever uploaded automatically — you attach the zip yourself.
- No telemetry, no analytics, no crash auto-upload (see docs/legal/PRIVACY.md).
- Supply chain: Dependabot + `dotnet list package --vulnerable --include-transitive` as a hard CI gate; CodeQL runs on every push via the shared workflow.
