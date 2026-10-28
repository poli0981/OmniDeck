# OmniDeck — Privacy Policy

Version 1.0 — effective from first public beta release.

**Short version: OmniDeck collects nothing.** There is no telemetry, no analytics, no account, no crash auto-upload, and no server operated by the OmniDeck project that receives your data.

## 1. Data stored locally on your device

All of the following stays in `%LocalAppData%\OmniDeck` and never leaves your machine unless you export or attach it yourself:

- Settings (language, UI preset, memory thresholds, per-app permissions).
- Module data: calendar events, stock watchlist and cached quotes, cached weather, media playlists.
- WebView2 profile data (cookies, site storage) for the web apps you sign in to — managed by the WebView2 Runtime on your device.
- Logs (7-day rolling) and any bug-report zips you generate.
- Your optional Twelve Data API key, encrypted with Windows DPAPI.

Uninstalling the app (or using Settings → Data → open data folder and deleting it) removes this data.

## 2. Network connections the app makes

OmniDeck only talks to the internet for the features you use:

- **Open-Meteo** — weather forecasts and geocoding for your saved locations.
- **Stooq** and (only if you configure a key) **Twelve Data** — stock quotes for tickers on your watchlist.
- **GitHub** — update checks/downloads (Velopack) and the daily web-app catalog refresh; opening a pre-filled bug-report issue happens in your browser and only if you choose to.
- **The third-party web apps you open** (Spotify, Discord, Google services, Facebook, etc.) — these connections go directly from your device to those providers. Their handling of your data is governed by **their** privacy policies, not this one. OmniDeck does not inject analytics into, read the contents of, or proxy these sessions.

## 3. Bug reports

Bug reporting is manual and transparent: the app shows you the pre-filled issue text, scrubs query strings/cookies/token-shaped values from captured console output and logs, and writes the log zip locally for you to review and attach yourself. Nothing is uploaded automatically.

## 4. Children

OmniDeck itself collects no data from anyone, including children. The third-party services accessible through it have their own age requirements.

## 5. Changes

Material changes to this policy will be presented in-app for re-acceptance. History is available in this repository's commit log.
