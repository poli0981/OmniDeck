# OmniDeck — P0 Spikes

P0 runs from 2 to 15 November 2026 ([ROADMAP](../ROADMAP.md#1-phases)). Its job is to de-risk everything that could invalidate the design before P1 builds on it. Each spike answers one question on real hardware and ends in a note in this folder with an explicit decision. P0 exits when all seven notes are merged. Progress is tracked in the [P0 milestone](https://github.com/poli0981/OmniDeck/milestone/1).

## 1. Rules

- **Spike code stays off `main`.** Proof-of-concept projects live on the `spike/p0` branch, which is pushed but never merged. The release SBOM is generated from `main`'s dependency graph ([`release.yml`](../../.github/workflows/release.yml)), and spike-only packages must not show up there, in Dependabot, or in dependency review. Each note names the commit it measured.
- **Spike data stays out of the repository.** WebView2 profiles (cookies, sign-ins), logs and measurements live under `%LocalAppData%\OmniDeck.Spikes`. They never go in the working tree, and never in the app's data root `%LocalAppData%\OmniDeck.Data`.
- **Secrets come from environment variables** (for example `OD_TWELVEDATA_KEY`). They are never committed, logged, or pasted into a note.
- **No circumvention.** No user-agent spoofing to get past sign-in checks, and no solving of bot challenges. If a service blocks an embedded browser or a scripted client, that is the result to record.
- **Sign-ins are done by hand** by the maintainer, in the spike app's window.
- **Record the environment.** Every note states the Windows build, the WebView2 Runtime version, the package versions and the hardware. The development machine runs a Windows Insider build with 32 GB of RAM, which is not what most users have, so memory numbers are re-measured on a GA build of Windows 11 if they look off.

## 2. Status

| # | Spike | Blocks | Decision | Note |
|---|---|---|---|---|
| 1 | DRM / Widevine in WebView2 | P2: Spotify catalog entry | **GO:** the runtime ships Widevine and Spotify plays, with no flags | [01-drm-widevine.md](01-drm-widevine.md) |
| 2 | Google sign-in and SSO popups | P2: `google` profile group, Spotify sign-in | Pending ([#3](https://github.com/poli0981/OmniDeck/issues/3)) | `02-google-sign-in.md` |
| 3 | Memory reality check and governor metric | **P1**: RAM governor ([§4](../RAM-GOVERNOR.md#4-cap-enforcement)) | Pending ([#4](https://github.com/poli0981/OmniDeck/issues/4)) | `03-memory.md` |
| 4 | CDP console capture | P2: bug-report flow | Pending ([#5](https://github.com/poli0981/OmniDeck/issues/5)) | `04-cdp-console.md` |
| 5 | Runtime preset swap | **P1**: theme engine | **GO with conditions:** `Od` styles for WPF-UI's static keys, motion driven from code, text size from the registry | [05-theme-swap.md](05-theme-swap.md) |
| 6 | LibVLCSharp in WPF (airspace, fullscreen) | P2: media player | Pending ([#7](https://github.com/poli0981/OmniDeck/issues/7)) | `06-libvlcsharp.md` |
| 7 | Stock data providers | P3: stocks | **GO with conditions:** Twelve Data with the user's key is the only provider; Stooq NO-GO | [07-providers.md](07-providers.md) |

The two spikes that block P1 (#3 and #5) go first. A spike that only blocks P2 or P3 may run on into the first weeks of P1, but its note still has to land before the phase it blocks starts.

## 3. Note template

Notes are named `NN-<slug>.md`. When a note is merged, its row in the table above links to it and records the decision, and the spike's issue is closed.

~~~markdown
# Spike NN — <title>

**Question:** the one thing this spike decides.
**Decision:** GO | GO with conditions | NO-GO, in one sentence.
**Measured:** YYYY-MM-DD on `spike/p0` at `<commit>`.

## Environment
Windows build, WebView2 Runtime, package versions, CPU / RAM / GPU.

## Method
What was run and how, in enough detail to repeat it.

## Results
Raw numbers (tables, or a CSV next to the note) and observations.

## Consequences
What changes in the spec, architecture, governor, catalog or roadmap, and any follow-up issues.
Changes to `docs/legal/**` and `catalog/webapps.json` are proposed here and made only after the
maintainer approves them.
~~~
