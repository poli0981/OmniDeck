# OmniDeck — Roadmap to v1.0 (January 2027)

Solo-developer plan, ~5.5 months from kickoff. Each phase has exit criteria; slipping triggers the cut order in §3, never a death march.

## 1. Phases

### P0 — Spikes (Jul 20 – Aug 2, 2026)
De-risk everything that could invalidate the design, on real hardware:

1. **DRM/Widevine:** load the Shaka Player support page and the Spotify Web Player in a bare WebView2 PoC. Record which key systems report available and whether Spotify actually plays.
2. **Google sign-in:** direct gmail.com login + SSO popup flow (`accounts.google.com`) in WebView2 with default UA.
3. **Memory reality check:** measure Active → `TrySuspendAsync`+`Low` → `Dispose` working sets for Discord, YouTube, Gmail; confirm the ladder's assumed numbers.
4. **CDP console capture:** `Runtime.consoleAPICalled` / `Log.entryAdded` ring buffer PoC.
5. **Theme swap:** runtime `ResourceDictionary` preset switching without visual artifacts.
6. **LibVLCSharp WPF:** airspace behavior — overlay controls + fullscreen toggle over `VideoView`. If blocked, evaluate FlyleafLib as the fallback engine.
7. **Providers:** hit Stooq CSV and Twelve Data free endpoints; freeze response models.

**Exit:** written go/no-go note per spike committed to `docs/spikes/`.

### P1 — Shell & skeleton (Aug 3 – Sep 27, 2026)
Shell (nav, tray, single-instance, command palette), module contract + host, RAM governor v1 with task-manager panel, theme engine + three presets (Standard polished, others functional), legal gate + About, settings + storage layer, and the three trivial modules: clock, timer, calculator. CI live (build, test, analyzers, CVE gate).

**Exit:** installable Velopack build; governor demotes/restores the trivial modules correctly; unit tests for governor + storage green.

### P2 — Web runner & core modules (Sep 28 – Nov 15, 2026)
Full web runner (profiles, nav policy, SSO popups, notifications→toast, permissions, downloads, suspend/hibernate, catalog fetch) with the v1 catalog; weather; calendar (local + .ics); media player v1 on LibVLCSharp. Bug-report flow end to end (CDP capture → scrubber → prefilled issue + zip).

**Exit:** dogfood daily — Gmail + Discord + YouTube stay signed in across hibernation; a full day's use stays under the hard cap.

### P3 — Stocks, presets, i18n (Nov 16 – Dec 20, 2026)
Stocks module (Stooq default + Twelve Data BYO key, watchlist, candlesticks); Dynamic and Comfort presets polished to spec (UI-MODES token table); VI + JA translations; settings import/export; accessibility pass (text scaling, High Contrast, keyboard nav).

**Exit:** feature-complete against SPEC v1 scope.

### P4 — Beta & release (Dec 21, 2026 – Jan 31, 2027)
**0.9 public beta** (~Dec 27) via GitHub Releases + community Discord; two stabilization cycles driven by bug reports; docs final pass; **v1.0 target Jan 31, 2027**.

## 2. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| Widevine unavailable in WebView2 on target machines | Spotify won't play | P0 spike gates the catalog entry; fallback: ship Spotify entry disabled with an explanatory note, YouTube covers music for many users |
| Google blocks sign-in in WebView2 | Google group unusable | P0 spike; fallback: session import guidance / treat as upstream limitation, documented |
| LibVLCSharp airspace friction in WPF | Media UX degraded | P0 spike; fallback engine FlyleafLib |
| Free API tiers change (Twelve Data/Stooq) | Stocks degraded | provider abstraction; BYO-key model isolates OmniDeck from quota policy |
| Solo bandwidth vs. other projects | Schedule slip | cut order below; phases sized with buffer |

## 3. Cut order if slipping

1. **Stocks** → v1.1 (self-contained, no other module depends on it).
2. Comfort/Dynamic polish reduced to "functional" (Standard stays polished).
3. JA translation → v1.1 (EN/VI ship).

The web runner, governor, legal gate, and the trivial modules are never cut — they *are* the product.

## 4. Post-v1 backlog (unordered)

Finnhub provider (real-time US), Google Calendar native sync, multi-point global hotkeys per web app, catalog entries from §4 of WEBAPP-CATALOG, per-module popout window session restore, portable build.
