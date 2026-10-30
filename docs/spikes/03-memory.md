# Spike 03 — Memory reality check and governor metric

**Question:** what do web apps really cost in each governor state, and which memory metric do the caps use?
**Decision:** GO. The governor measures **private commit** (`Process.PrivateMemorySize64`), summed over the shell and every WebView2 process. Suspension mostly trims the working set rather than freeing memory, so a working-set metric would reward exactly that trimming. The ladder's real memory saving comes from hibernation.
**Measured:** 2026-10-30 on `spike/p0`: the `WebLab --measure` run at `ec79454` (60 s phases, a sample every 2 s), the signed-in Gmail ladder at `3eae21e`, and the maintainer's own interactive session. Issue [#4](https://github.com/poli0981/OmniDeck/issues/4).

## Environment

- Windows Insider build 29648, i7-14700KF, 32 GB RAM, RTX 5080.
- WebView2 Runtime 156.0.4314.50. One shared environment and one profile per catalog `profile` group, hosted in WebLab, a plain WPF window standing in for the shell.
- Caveat: Chromium's memory heuristics depend on physical RAM, so absolute numbers on an 8 GB machine will differ.

## Method

- **Sampler.** Every 2 s, it reads the shell and each process from `CoreWebView2Environment.GetProcessInfos()`. For each process it records:
  - private commit (`Process.PrivateMemorySize64`);
  - private working set (`GetProcessMemoryInfo` → `PROCESS_MEMORY_COUNTERS_EX2.PrivateWorkingSetSize`, the figure Task Manager's Memory column shows);
  - working set;
  - CPU time.

  Renderers are attributed to apps through `GetProcessExtendedInfosAsync`, from each frame's root frame id and host.
- **Ladder per app.** Active → hidden → `TrySuspendAsync` + `MemoryUsageTargetLevel.Low` → resumed (`Normal` + `Resume` + shown) → disposed, keeping only `{url}` → reopened. The active, suspended and reopened phases last 60 s; the others last 30 s. Each figure is the mean of the phase's samples after the first two.
- **Everything at once.** All 8 catalog apps open, then every app except the visible one suspended, then all disposed.
- **Real use.** The maintainer's session: 11 app windows (Gmail three times), signed in, with Spotify and YouTube playing, for about 20 minutes.

## Results

### Ladder (MB)

"App" counts the processes attributed to the app. "Shared" counts the browser, GPU and utility processes.

**Discord, signed in**

| State | App commit | App private WS | Shared commit | Shared private WS | CPU (ms/s) |
|---|---|---|---|---|---|
| Active | 443 | 394 | 222 | 107 | 15.0 |
| Hidden | 307 | 268 | 171 | 96 | 2.6 |
| Suspended + Low | 291 | 93 | 170 | 49 | 2.0 |
| Resumed | 294 | 61 | 218 | 37 | 0.7 |
| Disposed | 0 | 0 | 0 | 0 | 0 |
| Reopened | 403 | 356 | 203 | 100 | 10.4 |

**YouTube home, signed out** (see [Open items](#open-items))

| State | App commit | App private WS | Shared commit | Shared private WS | CPU (ms/s) |
|---|---|---|---|---|---|
| Active | 206 | 182 | 260 | 124 | 14.8 |
| Hidden | 168 | 144 | 242 | 114 | 2.6 |
| Suspended + Low | 145 | 49 | 241 | 56 | 2.6 |
| Resumed | 165 | 57 | 258 | 40 | 4.6 |
| Disposed | 0 | 0 | 0 | 0 | 0 |
| Reopened | 180 | 164 | 263 | 123 | 3.1 |

**Gmail, signed in** (measured after the sign-in re-test in spike #2; still signed in after the reopen)

| State | App commit | App private WS | Shared commit | Shared private WS | CPU (ms/s) |
|---|---|---|---|---|---|
| Active | 253 | 234 | 269 | 130 | 19.4 |
| Hidden | 214 | 198 | 248 | 119 | 1.3 |
| Suspended + Low | 196 | 65 | 248 | 58 | 0.3 |
| Resumed | 221 | 148 | 261 | 39 | 17.6 |
| Disposed | 0 | 0 | 0 | 0 | 0 |
| Reopened | 255 | 236 | 254 | 122 | 25.7 |

- **Shell:** the WPF window itself used 93–130 MB of private commit throughout.
- **Suspend and resume:** `TrySuspendAsync` returned `true` in 0–4 ms, and resuming took 1–5 ms.
- **Reopen after dispose:** 155–176 ms until the controller was ready, and 590–900 ms until the first `NavigationCompleted` (2.5 s for signed-in Gmail).
- **One stall:** creating a controller while the previous browser process was still shutting down took 5.2 s once.

### Everything at once (MB)

| Scenario | Processes | Σ private commit | Σ private WS (≈ Task Manager) |
|---|---|---|---|
| 8 catalog apps open, idle (Discord signed in; Google, Spotify and Facebook signed out, see spike #2) | 17 | 2,246 | 1,552 |
| the same, with 7 of them suspended + Low | 17 | 2,103 | 853 |
| all disposed | 1 (shell) | 130 | 72 |
| maintainer's session: 11 windows signed in, 2 playing | 26 | 4,429 (peak 5,022) | 3,037 |

In the maintainer's session:

- The GPU process alone held 955 MB of private commit but only 335 MB of private working set.
- Widevine ran in three utility processes of 52–58 MB each.
- Task Manager showed 3,085 MB for the WebView2 group while the sampler read 2,960 MB of private working set for the WebView2 processes. The sampler matches what users see.

## Analysis

- **Hiding** a web app already releases 15–31 % of its committed memory, because Chromium drops caches for hidden pages.
- **Suspend + Low** cuts the private working set by 65–70 % but committed memory by only 5–15 %. The pages move to the standby list or the page file; they are not released. After resume the working set stays low until those pages are touched again.

  A working-set metric would therefore read low exactly when nothing has been freed. That is the page shuffling [RAM-GOVERNOR](../RAM-GOVERNOR.md#4-cap-enforcement) §4 rejects.
- **Only disposal releases memory.** Disposing the *last* web app ends the whole WebView2 browser process. That is the only way the shared overhead goes away: 170–260 MB when idle, and about 1.5 GB of commit (mostly GPU) in the maintainer's media session.
- **The ladder's assumption was wrong:** RAM-GOVERNOR §1 assumed "tens of MB" for a suspended web app. Measured, its committed memory stays at 85–95 % of the hidden state.

## Consequences

- **Metric:** private commit, summed over the shell and every WebView2 process. `Process.PrivateMemorySize64` needs no process handle. The task-manager panel labels the figure as committed memory and may show the private working set beside it, since that is the number Task Manager users know.
- **Victims:** cap enforcement picks apps by their attributed commit. Shared processes appear as a separate "WebView2 (shared)" row and are never demoted.
- **Ladder:** the Suspended rung saves CPU and power (2 ms/s instead of about 15 ms/s), not memory. Memory pressure goes straight to Hibernate. RAM-GOVERNOR §1 and §4 are updated at P0 exit.
- **Caps:** the maintainer's real session reached 4.4–5.0 GB committed, right at the 4.5 GiB soft cap. That is fine on 32 GB but not on 8 GB. Proposal for P1: default caps relative to physical memory, for example soft = min(4.5 GiB, 35 % of RAM) and hard = min(6 GiB, 50 %), re-checked while dogfooding in P2.
- **Last app:** hibernating the last web app costs a browser restart on the next open (about 160 ms plus the page load). The web runner should avoid disposing the last app and immediately opening another, where a 5 s stall was seen.
- **GPU process:** its commit balloons with video (955 MB), and it is shared, so it inflates the total without being something the governor can demote. When shared processes alone push the total over the hard cap, the hard-cap toast says so instead of hibernating apps that would free nothing.

## Open items

- ~~**Gmail:** Gmail signed in was not measured, because the profile had lost its session (spike #2).~~ Done after the sign-in re-test; see the Gmail table above. The same pattern holds: hiding −15 % commit, suspend + Low −8 % more commit but −67 % private working set.
- **8 GB machines:** every number here comes from a 32 GB machine. Re-measure on a GA build of Windows 11 with 8 GB of RAM during P2 dogfooding.

## Reproduce

On `spike/p0`:

```powershell
dotnet run --project spikes/WebLab -- --measure --phase=60   # CSV in %LocalAppData%\OmniDeck.Spikes\memory\
```
