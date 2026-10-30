# OmniDeck — Privacy Policy

Version 1.1 — effective from the first release after 0.0.1-alpha.1. Changes since 1.0: the Stooq stock data service is no longer used, and Messenger is no longer in the web app catalog.

**Short version: OmniDeck collects nothing.** There is no telemetry, no analytics, no account, no crash auto-upload, and no server operated by the OmniDeck project that receives your data.

## 1. Data stored locally on your device

All of the following stays in `%LocalAppData%\OmniDeck.Data` (the data root) and never leaves your machine unless you export or attach it yourself:

- Settings (language, UI preset, memory thresholds, per-app permissions).
- Module data: calendar events, stock watchlist and cached quotes, cached weather, media playlists.
- WebView2 profile data (cookies, site storage) for the web apps you sign in to — managed by the WebView2 Runtime on your device.
- Logs (7-day rolling) and any bug-report zips you generate.
- Your optional Twelve Data API key, encrypted with Windows DPAPI.

The app itself is installed separately, in `%LocalAppData%\OmniDeck`. Uninstalling OmniDeck removes both folders. To reset the app without uninstalling it, delete the data root (Settings → Data → open data folder).

## 2. Network connections the app makes

OmniDeck only talks to the internet for the features you use:

- **Open-Meteo** — weather forecasts and geocoding for your saved locations.
- **Twelve Data**, only after you enter your own API key: stock quotes, price history and symbol search for the stocks module.
- **GitHub** — update checks/downloads (Velopack) and the daily web-app catalog refresh; opening a pre-filled bug-report issue happens in your browser and only if you choose to.
- **Microsoft Edge WebView2 Runtime** — the Microsoft component that renders the web apps may contact Microsoft services on its own, for example Microsoft Defender SmartScreen reputation checks for the pages you visit and the runtime's own updates and diagnostic data. That traffic is governed by Microsoft's privacy statement and your Windows diagnostic-data settings; OmniDeck does not enable any additional reporting.
- **The third-party web apps you open** (Spotify, Discord, Google services, Facebook, etc.) — these connections go directly from your device to those providers. Their handling of your data is governed by **their** privacy policies, not this one. OmniDeck does not inject analytics into, read the contents of, or proxy these sessions.

## 3. Bug reports

Bug reporting is manual and transparent: the app shows you the pre-filled issue text, scrubs query strings/cookies/token-shaped values from captured console output and logs, and writes the log zip locally for you to review and attach yourself. Nothing is uploaded automatically.

## 4. Children

OmniDeck itself collects no data from anyone, including children. The third-party services accessible through it have their own age requirements.

## 5. Changes

Material changes to this policy will be presented in-app for acknowledgment. History is available in this repository's commit log.

## 6. Contact

Privacy questions: privacy@poli0981.dev.
