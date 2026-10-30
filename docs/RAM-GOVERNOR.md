# OmniDeck — RAM Governor

The governor is the feature that makes "many apps in one" viable. It moves every module down a ladder of states the longer it goes unused, and enforces a total memory ceiling.

## 1. State ladder

| State | Entered when | Native module | Web app | Measured web-app cost (committed, P0 spike #3) |
|---|---|---|---|---|
| Active | visible / interacted | full UI | full WebView2 | app 250–450 MB, plus 170–270 MB of shared browser, GPU and utility processes |
| Idle | hidden < `IdleAfter` | keep view, stop animations/timers | hidden, still live | 15–31 % less, as Chromium drops the hidden page's caches |
| Suspended | hidden ≥ `IdleAfter` | `SuspendAsync`: drop caches, charts, bitmaps; GC follows | `TrySuspendAsync` + `MemoryUsageTargetLevel.Low` | 85–95 % of Idle. The working set is trimmed by about 65 %, so this rung saves CPU and power, not memory |
| Hibernated | hidden ≥ `HibernateAfter`, or cap pressure | `HibernateAsync`: view + everything released, tiny state blob kept | `Dispose()`; only `{url}` kept, session lives in profile cookies | 0. Disposing the *last* web app also ends the shared processes |
| Closed | user closes | released | released | 0 |

Reactivation from Suspended is instant. From Hibernated it is a cold open (view rebuild and page reload, about 0.6–2.5 s); that is acceptable because the profile keeps the user signed in. After the last web app has been hibernated, the next one also restarts the browser process (about 160 ms).

## 2. Defaults (all configurable in Settings → Memory)

| Setting | Default |
|---|---|
| `IdleAfter` | 5 min hidden |
| `HibernateAfter` | 30 min hidden |
| `SoftCapBytes` | min(4.5 GiB, 35 % of physical RAM) |
| `HardCapBytes` | min(6.0 GiB, 50 % of physical RAM) |
| `PollInterval` | 10 s |

## 3. Pinning

A module is **pinned** (governor must not demote it below its current state) when any of:

- `CanSuspendAsync()` returns false (media player during playback);
- the web app's `IsDocumentPlayingAudio` is true **and** its catalog entry has `audioPin: true` (Spotify, YouTube);
- the user pinned it manually in the task-manager panel.

Pinned modules are skipped by cap enforcement; if the hard cap is exceeded purely by pinned modules, the governor toasts a warning instead of killing audio.

## 4. Cap enforcement

Total footprint = shell process + Σ all WebView2 processes (`CoreWebView2Environment.GetProcessInfos()` → PID), sampled every `PollInterval`.

**Metric: private commit** (`Process.PrivateMemorySize64`), summed over the shell and every WebView2 process. P0 spike #3 ([note](spikes/03-memory.md)) chose it for three reasons:
- Summing `Process.WorkingSet64` over-counts, because Chromium processes share DLL and shared-memory pages and each one reports them again.
- Working sets drop whenever pages are trimmed. Suspend + `Low` cut the private working set by 65–70 % but committed memory by only 5–15 %, so a working-set cap would reward exactly the page shuffling the honesty note below rejects.
- Private commit needs no process handle.

The caps, the toasts and the task-manager panel all use it. The panel may show the private working set beside it, since that is the figure Task Manager shows. Renderer processes are attributed to apps through `GetProcessExtendedInfosAsync`. The shared browser, GPU and utility processes form one "WebView2 (shared)" row and are never a demotion target.

- **> SoftCap:** hibernate the least-recently-used unpinned module, since Suspended frees almost no memory. Repeat on the next tick if still above.
- **> HardCap:** force-hibernate LRU unpinned modules until below, then raise one toast summarizing what was hibernated. If the shared WebView2 processes alone keep the total above the cap (the GPU process grows with video), the toast says so instead of hibernating apps that would free nothing.

Ordering hint: `RamClass.Heavy` modules (web apps, media, stocks charts) are preferred victims over `Light` ones at equal recency. Recency counts in poll intervals, so modules hidden within the same interval are equally recent.

Honesty note: for in-process native modules the real win comes from modules *actually releasing* resources in `SuspendAsync`/`HibernateAsync`. The governor performs a `GC.Collect` + LOH compaction after demotions, and deliberately does **not** call `EmptyWorkingSet` tricks — those only shuffle pages to standby and fake the numbers.

## 5. Task-manager panel

Settings → Memory → "Running modules": live table of every module with state, memory estimate (the governor metric), last-used time, pin toggle, and Suspend / Hibernate / Close buttons. This is both a user feature and the primary debugging tool for the governor itself; the panel reads and writes the same scheduler, no side channel.

As built in P1, the panel sits in Settings → Memory under the threshold fields, and the command palette's "Running modules" opens it. Native modules share the shell process, so the panel shows OmniDeck's total against both caps rather than a per-module figure; web apps get their own figures with the web runner (P2). The pin toggle is labelled "Keep running", so it is not confused with pinning a module to the sidebar, and it is stored in `settings.json` (`memory.keepRunning`) next to the thresholds.

## 6. Testing

The scheduler is pure logic over an injected clock and an injected memory sampler — unit-test every transition, cap scenario, and pinning rule in `OmniDeck.Tests` without touching real WebView2.
