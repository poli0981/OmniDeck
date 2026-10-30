# Spike 01 — DRM / Widevine in WebView2

**Question:** does WebView2 play Widevine-protected media — concretely, does the Spotify Web Player play?
**Decision:** GO. The evergreen WebView2 Runtime ships its own Widevine CDM. With default settings and no browser arguments, EME offers Widevine (software) and PlayReady, and the Spotify Web Player played after sign-in.
**Measured:** 2026-10-30 on `spike/p0` at `0fe4b12` (`spikes/WebLab`: the `--auto` EME probe, plus a session where the maintainer signed in by hand). Issue [#2](https://github.com/poli0981/OmniDeck/issues/2).

## Environment

- WebView2 Runtime 156.0.4314.50, evergreen and machine-wide, with WebView2 SDK 1.0.4314.35.
- The runtime folder contains `WidevineCdm\_platform_specific\win_x64\widevinecdm.dll` (CDM 4.10.3050.1, with its `.sig`), and VMP signature files sit next to `msedgewebview2.exe` and `msedge.dll`.
- Windows Insider build 29648, RTX 5080.
- Spotify: the maintainer's own account, signed in with Spotify credentials.

## Method

- **EME probe page** (`spikes/WebLab/LabPages/eme.html`): on a bare profile with default environment options, call `navigator.requestMediaKeySystemAccess` and then `createMediaKeys` for each key system and robustness, with H.264 video and AAC audio.
- **Shaka Player** support page, checked by eye.
- **Spotify Web Player** on its own profile: sign in, then play, pause, resume and change tracks over about 20 minutes. WebLab logs `IsDocumentPlayingAudioChanged`; processes were checked in Task Manager.

## Results

| Key system | Robustness | Available |
|---|---|---|
| `com.widevine.alpha` | (none) | yes |
| `com.widevine.alpha` | `SW_SECURE_CRYPTO` | yes |
| `com.widevine.alpha` | `HW_SECURE_ALL` | no |
| `com.microsoft.playready.recommendation` | (none) | yes |
| `com.microsoft.playready.recommendation` | `3000` | no |
| `com.microsoft.playready` | (none) | yes |
| `org.w3.clearkey` | (none) | yes |

- **Spotify plays.**
  - Sign-in stayed inside `accounts.spotify.com`.
  - Playback started 11 s after landing on `open.spotify.com` and kept going for about 20 minutes. `IsDocumentPlayingAudio` toggled at each track change.
  - Task Manager showed three "WebView2 Utility: Widevine" processes of 52–58 MB each (private working set) while Spotify, YouTube and the EME probe page were open.
- **No setup needed:** no browser argument or setting was required.
- **Hardware-backed DRM** (Widevine `HW_SECURE_ALL`, PlayReady SL3000) is not offered. That limits HD playback on services that demand it. None of those are in the catalog.

## Consequences

- The `spotify` entry stays in the v1 catalog. The "gated on the P0 spike" note in [WEBAPP-CATALOG](../WEBAPP-CATALOG.md#3-initial-catalog-v1) §3 can go at P0 exit.
- **Governor:** the Widevine processes are utility processes that count toward the footprint while media plays. `audioPin` already keeps Spotify active during playback.
- **Future runtimes:** the CDM ships with the runtime and updates with it. If a future runtime breaks EME, the bug-report flow should be able to tell; an EME self-check (the probe page's logic) is a cheap addition to the report.
- **Spotify's third-party sign-in buttons** (Google, Facebook, Apple) belong to spike #2.

## Reproduce

On `spike/p0`:

```powershell
dotnet run --project spikes/WebLab -- --auto   # EME results in %LocalAppData%\OmniDeck.Spikes\weblab\*.log
dotnet run --project spikes/WebLab             # open "spotify", sign in, play
```
