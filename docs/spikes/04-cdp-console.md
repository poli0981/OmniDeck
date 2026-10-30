# Spike 04 — CDP console capture

**Question:** can the web runner keep a per-app console ring buffer through the DevTools Protocol, safely and cheaply?
**Decision:** GO. Capture through `Runtime.enable` + `Log.enable` works with the DevTools UI disabled. The widely used page-side check did not detect it, its cost was too small to measure, and signed-in apps kept working with it on. Capture stays on for every web app.
**Measured:** 2026-10-30 on `spike/p0`: the `WebLab --auto` run (`0fe4b12`), the `--measure` run (`ec79454`) and the `--check-signed-in` run (`3eae21e`). Issue [#5](https://github.com/poli0981/OmniDeck/issues/5).

## Environment

- WebView2 Runtime 156.0.4314.50, with `AreDevToolsEnabled = false` (the release setting) on every view.
- The capture subscribes to `Runtime.consoleAPICalled`, `Runtime.exceptionThrown` and `Log.entryAdded` through `GetDevToolsProtocolEventReceiver`, after `CallDevToolsProtocolMethodAsync("Runtime.enable" / "Log.enable")`.
- It feeds a 200-entry ring buffer per app.

## Method

- **Detection page** (`LabPages/cdp-detect.html`): logs an `Error` whose `stack` getter has a side effect. Only a DevTools client that serializes console arguments would read it. The page then emits `console.log`, `console.warn`, an uncaught exception and a failing fetch. It is checked with capture off, with Runtime + Log, and with Log only.
- **Signed-in apps:**
  - Discord, Gmail and Spotify run with capture off for 60 s, on for 60 s, then reload with capture on;
  - separately, signed-in Gmail reloads with capture on.

## Results

| Mode | Detected by the page? | Captured |
|---|---|---|
| off | no | nothing |
| Runtime + Log | **no** | `console.debug/log/warn/error`, uncaught exceptions (`Runtime.exceptionThrown`), network errors such as a 404 (`Log.entryAdded`) |
| Log only | no | network and browser messages only; **not** `console.*` calls |

- **DevTools UI:** capture works with it disabled. Turning capture on took 4–24 ms per app (230 ms once, on Spotify).
- **Signed-in apps:**
  - Discord, signed in, reloaded fine with capture on;
  - Gmail, signed in, reloaded with capture on and stayed signed in, with 12 entries captured;
  - no captcha or sign-out appeared.
- **Cost:**
  - Across the three apps, committed memory was 1,057 MB with capture off, 1,041 MB with it on, and 988 MB after the reloads. The difference is within noise.
  - CPU was dominated by the apps themselves.
  - The buffer itself is at most 200 short strings per app.
- **Volume:**
  - Discord logged 86 entries in about 2 minutes, mostly debug lines styled with `%c` CSS arguments. A 200-entry ring holds about 4–5 minutes of Discord.
  - Gmail and Spotify were far quieter: interventions, and Tracking Prevention warnings on Spotify's login page.
- **Personal data:** console lines carry it. Discord logs the user's numeric ID, for example.
- **Not the alternative:** Chromium's own `--enable-logging` also records console messages, but in one global file shared by every app, including personal data. It is a diagnostic switch, not the capture mechanism.

## Consequences

- **Always on:** the web runner enables `Runtime` + `Log` for each web app when its view is created, and keeps the DevTools UI disabled. Capture stops with the view when it is disposed.
- **Two buffers per app:** a 200-entry ring for everything, and a 50-entry ring for errors and uncaught exceptions only, so debug chatter cannot evict the lines a bug report needs.
- **Formatting:** apply `%c` / `%s` format arguments when rendering, and drop the CSS.
- **Privacy:** console lines go through the bug-report scrubber (SPEC §6) like the logs do. Nothing is captured to disk until the user starts a report.
- **Watch:** this held on runtime 156. If a provider starts flagging `Runtime.enable` (captchas, sign-outs), the fallback is to enable capture only while a bug report is being recorded.

## Reproduce

On `spike/p0`:

```powershell
dotnet run --project spikes/WebLab -- --auto             # detection and capture modes
dotnet run --project spikes/WebLab -- --check-signed-in  # signed-in Gmail reload with capture on
```
