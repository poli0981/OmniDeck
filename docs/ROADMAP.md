# OmniDeck — Roadmap to v1.0 (May 2027)

Solo-developer plan, ~29 weeks from the start of P0 (2 November 2026). Each phase has exit criteria; slipping triggers the cut order in §4, never a death march.

> **Re-baselined 2026-10-28.** The first draft assumed a 20 July 2026 start and a January 2027 release. Development actually starts on 2 November 2026, so every phase keeps its original length and moves; P2 gains one buffer week for Tết (Lunar New Year, 6 February 2027). Windows 10 support was dropped at the same time ([SPEC](SPEC.md) §1).
>
> **Re-baselined 2026-11-02.** P0 and P1 both finished early, on 30 October 2026, so P2 started on 2 November 2026 instead of 11 January 2027. P3 and P4 keep their dates until P2 exits, and are re-baselined then.

## 1. Phases

### Kickoff (28 Oct 2026) — done

Repository foundation: documentation suite; solution skeleton (`OmniDeck.App`, `OmniDeck.Core`, `OmniDeck.Tests`); self-contained CI (build, tests, formatting, analyzers as errors, NuGet Audit, catalog validation, workflow and link linting, CodeQL, SonarCloud, Dependency Review, OpenSSF Scorecard); protected `main` and release tags; the Velopack release pipeline proven end to end with the `0.0.1-alpha.1` pre-release.

### P0 — Spikes (2 – 15 Nov 2026) — done early, 30 Oct 2026

De-risk everything that could invalidate the design, on real hardware:

1. **DRM/Widevine:** load the Shaka Player support page and the Spotify Web Player in a bare WebView2 PoC. Record which key systems report available and whether Spotify actually plays.
2. **Google sign-in:** direct gmail.com login + SSO popup flow (`accounts.google.com`) in WebView2 with default UA; also Spotify's third-party sign-in buttons (Google/Facebook/Apple).
3. **Memory reality check:** measure Active → `TrySuspendAsync`+`Low` → `Dispose` for Discord, YouTube, Gmail; confirm the ladder's assumed numbers and choose the governor's memory metric ([RAM-GOVERNOR](RAM-GOVERNOR.md) §4).
4. **CDP console capture:** `Runtime.consoleAPICalled` / `Log.entryAdded` ring buffer PoC.
5. **Theme swap:** runtime `ResourceDictionary` preset switching without visual artifacts.
6. **LibVLCSharp WPF:** airspace behavior — overlay controls + fullscreen toggle over `VideoView`. If blocked, evaluate FlyleafLib as the fallback engine.
7. **Providers:** hit Stooq CSV and Twelve Data free endpoints; freeze response models.

**Exit:** a written go/no-go note per spike in [`docs/spikes/`](spikes/README.md). The proof-of-concept code stays on the unmerged `spike/p0` branch, so spike-only packages never reach `main`'s dependency graph or the release SBOM.

**Result:** all seven notes are in. #1, #3 and #4 are GO; #2, #5, #6 and #7 are GO with conditions:
- #1 DRM: Widevine ships with the runtime.
- #2 sign-in: works and persists; Google's country-code hop needs `navAllowSets` in P2.
- #3 memory: the governor metric is private commit; suspend trims, only hibernate frees.
- #4 CDP capture: always on.
- #5 presets: code-built, with `Od` styles over WPF-UI's static keys.
- #6 libVLC: fullscreen in place; pop-out needs a vout restart and a seek.
- #7 providers: Twelve Data with the user's own key; Stooq dropped.

### P1 — Shell & skeleton (16 Nov 2026 – 10 Jan 2027) — done early, 30 Oct 2026

Shell (nav, tray, single-instance, command palette), module contract + host, RAM governor v1 with task-manager panel, theme engine + three presets (Standard polished, others functional), legal gate + About, settings + storage layer (data root, Velopack uninstall hook), and the three trivial modules: clock, timer, calculator.

**Exit (0.1.0):** installable Velopack build; governor demotes/restores the trivial modules correctly; unit tests for governor + storage green.

As delivered: eight work packages (PRs #20–#25, #27 and #29) and three releases (`0.1.0-alpha.1`, `0.1.0-alpha.2`, `0.1.0`), all three exit criteria met. Settings import/export, planned for P3, shipped with it. Carried forward:

- **P3 presets:** Comfort's AAA body text and Dynamic's gradient accents (docs/UI-MODES.md §2).
- **P3 accessibility:** UI Automation exposes WPF-UI 4.3's navigation items as data items with no Invoke or Select pattern; keyboard users reach them with Tab and Enter. The accent swatches are announced by their hex codes.
- **P2:** pop-out module windows, deferred by the P1 plan.

### P2 — Web runner & core modules (from 2 Nov 2026; planned 11 Jan – 7 Mar 2027)

Full web runner (profiles, nav policy, SSO popups, notifications→toast, permissions, downloads, suspend/hibernate, catalog fetch) with the v1 catalog; weather; calendar (local + .ics); media player v1 on LibVLCSharp. Bug-report flow end to end (CDP capture → scrubber → prefilled issue + zip).

**Exit (0.2.0):** dogfood daily — Gmail + Discord + YouTube stay signed in across hibernation; a full day's use stays under the hard cap.

As delivered so far: the `0.1.1` hotfix (a tray icon that leaked GDI objects), then the plan's ten work packages and two catalog pull requests, released as `0.2.0-alpha.1` and `0.2.0-alpha.2` on 2 November 2026. `0.2.0` follows about a week of dogfooding against the exit criterion. Changed on the way:

- Downloads use WebView2's own flyout rather than a progress row of OmniDeck's, and permission questions sit in a bar above the page, because no dialog can cover WebView2 ([SPEC](SPEC.md) §4).
- Web apps with notifications on stay resident: the time ladder skips them ([RAM-GOVERNOR](RAM-GOVERNOR.md) §3).
- Pop-out windows move the module's view, so web pages and videos keep running through the move ([ARCHITECTURE](ARCHITECTURE.md) §2).
- Left for later: web app favicons, `.ics` subscriptions (they would need a privacy policy update) and Discord's global push-to-talk hotkey (all in §5), and the weather charts, which wait for ScottPlot in P3.

### P3 — Stocks, presets, i18n (8 Mar – 11 Apr 2027)

Stocks module (Twelve Data with the user's own key, which spike #7 left as the only provider; watchlist, candlesticks); Dynamic and Comfort presets polished to spec (UI-MODES token table); VI + JA translations; accessibility pass (text scaling, contrast themes, keyboard nav).

**Exit (0.3.0):** feature-complete against SPEC v1 scope.

### P4 — Beta & release (12 Apr – 23 May 2027)

**0.9 public beta** (~18 Apr) via GitHub Releases + the community Discord; two stabilization cycles driven by bug reports; docs final pass; **v1.0 target 23 May 2027**.

## 2. Versioning

| Milestone | Version |
|---|---|
| Kickoff build | `0.0.1-alpha.1` |
| P0 — spikes | no build: the app does not change |
| Interim builds during phase N (P1–P3) | `0.N.0-alpha.M` |
| P1 exit — first installable shell | `0.1.0` |
| P2 exit | `0.2.0` |
| P3 exit — feature-complete | `0.3.0` |
| Public beta | `0.9.0` (fixes as `0.9.x`) |
| Release | `1.0.0` |

Every version below 1.0.0 is published as a GitHub **pre-release**; 1.0.0 is the first stable release. Releases come from GPG-signed `vX.Y.Z[-suffix]` tags on `main` ([RELEASING](../RELEASING.md)).

## 3. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| Widevine unavailable in WebView2 on target machines | Spotify won't play | **Retired by spike #1:** the evergreen runtime ships Widevine and Spotify plays without flags. If a future runtime breaks EME, the bug report's EME self-check shows it. Fallback unchanged: ship the Spotify entry disabled with a note |
| Google blocks sign-in in WebView2 | Google group unusable | **Spike #2:** sign-in works with the default user agent, and one sign-in covers the `google` group. Google's hop to country-code domains needs `navAllowSets` (P2). Client hints name WebView2, so Google could still block it later; fallback: treat it as an upstream limitation, documented |
| LibVLCSharp airspace friction in WPF | Media UX degraded | **Retired by spike #6:** overlay controls and fullscreen in place work in a `FluentWindow`. Pop-out, in P2, moves the same view, so the video keeps playing and the overlay follows. FlyleafLib is not needed |
| Free API tiers change (Twelve Data/Stooq) | Stocks degraded | **Materialized for Stooq** (2026-10-30), which now blocks scripted clients. Spike #7 leaves Twelve Data with the user's own key as the only provider; its free plan covers US listings. The provider abstraction stays; Stocks is first in the cut order (§4) |
| Web sessions lost across restarts | Users signed out of web apps | Seen once in P0 and not reproduced (spike #2). Since P2, quitting closes the web apps cleanly so the browser writes its cookies out; sign-in health logs each profile's cookie count at close and at first load and warns when it drops to none; bug reports carry the counts (never values) |
| User data stored in Velopack's install folder | Uninstall or reinstall wipes data and sign-ins | data root `%LocalAppData%\OmniDeck.Data`, never `%LocalAppData%\OmniDeck` ([ARCHITECTURE](ARCHITECTURE.md) §5) |
| Solo bandwidth vs. other projects | Schedule slip | cut order below; phases sized with buffer |

## 4. Cut order if slipping

1. **Stocks** → v1.1 (self-contained, no other module depends on it).
2. Comfort/Dynamic polish reduced to "functional" (Standard stays polished).
3. JA translation → v1.1 (EN/VI ship).

The web runner, governor, legal gate, and the trivial modules are never cut — they *are* the product.

## 5. Post-v1 backlog (unordered)

Finnhub provider (real-time US), Google Calendar native sync, `.ics` calendar subscriptions, multi-point global hotkeys per web app (Discord's push-to-talk among them), web app favicons in the sidebar, catalog entries from §4 of WEBAPP-CATALOG, per-module popout window session restore, portable build.
