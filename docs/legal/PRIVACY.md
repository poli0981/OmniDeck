# OmniDeck — Privacy Policy

Version 1.2 — effective from 0.3.0. Changes since 1.1: what Open-Meteo and Twelve Data receive is spelled out, requests OmniDeck makes itself name the app and its version, and media snapshots, saved in your Pictures folder, are not removed with the app.

**Short version: OmniDeck collects nothing.** There is no telemetry, no analytics, no account, no crash auto-upload, and no server operated by the OmniDeck project that receives your data.

## 1. Data stored locally on your device

All of the following stays in `%LocalAppData%\OmniDeck.Data` (the data root) and never leaves your machine unless you export or attach it yourself:

- Settings (language, UI preset, memory thresholds, per-app permissions).
- Module data: calendar events, stock watchlist and cached quotes, cached weather, media playlists.
- WebView2 profile data (cookies, site storage) for the web apps you sign in to — managed by the WebView2 Runtime on your device.
- Logs (7-day rolling) and any bug-report zips you generate.
- Your optional Twelve Data API key, encrypted with Windows DPAPI.

Snapshots you take in the media player are saved as pictures in `Pictures\OmniDeck`, outside the data root.

The app itself is installed separately, in `%LocalAppData%\OmniDeck`. Uninstalling OmniDeck removes both folders, but not the snapshots in `Pictures\OmniDeck`: those are yours to keep or delete, like any picture. To reset the app without uninstalling it, delete the data root (Settings → Data → open data folder).

## 2. Network connections the app makes

OmniDeck only talks to the internet for the features you use:

- **Open-Meteo** — weather forecasts and geocoding for your saved locations. It receives the place names you search for, with the UI language for the results, and the coordinates of your saved places, rounded to four decimal places (about 10 m), with the units you chose. No key or identifier is sent.
- **Twelve Data**, only after you enter your own API key: stock quotes, price history and symbol search for the stocks module. It receives your key (in a request header, never in a web address), the symbols on your watchlist and the text you search for, and can link them to your Twelve Data account.
- **GitHub** — update checks/downloads (Velopack) and the daily web-app catalog refresh; opening a pre-filled bug-report issue happens in your browser and only if you choose to.
- **Microsoft Edge WebView2 Runtime** — the Microsoft component that renders the web apps may contact Microsoft services on its own, for example Microsoft Defender SmartScreen reputation checks for the pages you visit and the runtime's own updates and diagnostic data. That traffic is governed by Microsoft's privacy statement and your Windows diagnostic-data settings; OmniDeck does not enable any additional reporting.
- **The third-party web apps you open** (Spotify, Discord, Google services, Facebook, etc.) — these connections go directly from your device to those providers. Their handling of your data is governed by **their** privacy policies, not this one. OmniDeck does not inject analytics into, read the contents of, or proxy these sessions.

Requests OmniDeck makes itself — to Open-Meteo, to Twelve Data and for the web-app catalog — name the app and its version in their User-Agent header (for example `OmniDeck/0.3.0`), as these services ask of apps; they carry nothing else about you or your device.

## 3. Bug reports

Bug reporting is manual and transparent: the app shows you the pre-filled issue text, scrubs query strings/cookies/token-shaped values from captured console output and logs, and writes the log zip locally for you to review and attach yourself. Nothing is uploaded automatically.

## 4. Children

OmniDeck itself collects no data from anyone, including children. The third-party services accessible through it have their own age requirements.

## 5. Changes

Material changes to this policy will be presented in-app for acknowledgment. History is available in this repository's commit log.

## 6. Contact

Privacy questions: privacy@poli0981.dev.
