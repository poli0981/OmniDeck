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

A module in a window of its own (a pop-out) is visible while that window is open, so it stays Active whatever the main window shows; closing the window puts it back on the ladder as a hidden module.

The main window's module is hidden too while that window is minimized or hidden in the tray: it goes Idle when the window goes away and down the ladder from there, and it is active again when the window comes back (rebuilt, if it was hibernated meanwhile). Pop-outs keep their rule above, even when their own window is minimized.

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

A module that works while hidden is **resident** (`IOdResidency`): in P2, a web app with notifications on, which would stop notifying once suspended. The time ladder skips it; the caps may still hibernate it, after every other victim, and then OmniDeck shows a notification naming the app, which reopens it when clicked.

Pinned modules are skipped by cap enforcement; if the hard cap is exceeded purely by pinned modules, the governor toasts a warning instead of killing audio.

Time spent pinned does not count as time hidden: each pass restarts a pinned module's hidden time, so a player that stops after an hour out of sight is suspended `IdleAfter` later and hibernated `HibernateAfter` later, not on the next pass.

## 4. Cap enforcement

Total footprint = shell process + Σ all WebView2 processes, sampled every `PollInterval`.

**Metric: private commit** (`Process.PrivateMemorySize64`), summed over the shell and every WebView2 process. P0 spike #3 ([note](spikes/03-memory.md)) chose it for three reasons:
- Summing `Process.WorkingSet64` over-counts, because Chromium processes share DLL and shared-memory pages and each one reports them again.
- Working sets drop whenever pages are trimmed. Suspend + `Low` cut the private working set by 65–70 % but committed memory by only 5–15 %, so a working-set cap would reward exactly the page shuffling the honesty note below rejects.
- Private commit needs no process handle.

The caps, the toasts and the task-manager panel all use it. The panel may show the private working set beside it, since that is the figure Task Manager shows. Renderer processes are attributed to apps through `GetProcessExtendedInfosAsync`. The shared browser, GPU and utility processes form one "WebView2 (shared)" row and are never a demotion target.

- **> SoftCap:** hibernate the least-recently-used unpinned module, since Suspended frees almost no memory. Repeat on the next tick if still above.
- **> HardCap:** force-hibernate LRU unpinned modules until below, then raise one toast summarizing what was hibernated. If the shared WebView2 processes alone keep the total above the cap (the GPU process grows with video), the toast says so instead of hibernating apps that would free nothing.

Ordering hint: `RamClass.Heavy` modules (web apps, media, stocks charts) are preferred victims over `Light` ones at equal recency. Recency counts in poll intervals, so modules hidden within the same interval are equally recent.

As built in P2:

- **Measuring.** `IOdMemory` measures the shell; every `IOdMemoryReporter` adds the processes it follows. The web runner's `OdWebMemoryReporter` lists the WebView2 processes with `GetProcessExtendedInfosAsync` on each pass. A renderer counts for the app whose page it hosts: matched by main frame id (`CoreWebView2.FrameId`), or by host when only one open app shows that host. A renderer hosting pages of several apps is split evenly between them. The browser, GPU and utility processes, and pages no open app shows, count as shared. A reporter that fails is left out of that pass.
- **Victim order:** resident modules last, then least recently used, then `Heavy` first, then the module that uses more.
- **After a hibernation:** a web app's processes take a moment to exit. The pass therefore counts its figure as gone at once (and the shared processes too, when it was the last web app), and the next pass measures again. This keeps the hard-cap loop from hibernating more apps than needed.
- **Shared processes at the hard cap:** the governor checks whether the shell, the shared processes and the web apps that are not victims (on screen, kept running, or refusing) are above the hard cap on their own. If they are, it hibernates nothing and its one notification for that excursion says why.

Honesty note: for in-process native modules the real win comes from modules *actually releasing* resources in `SuspendAsync`/`HibernateAsync`. The governor performs a `GC.Collect` + LOH compaction after demotions, and deliberately does **not** call `EmptyWorkingSet` tricks — those only shuffle pages to standby and fake the numbers.

## 5. Task-manager panel

Settings → Memory → "Running modules": live table of every module with state, memory estimate (the governor metric), last-used time, pin toggle, and Suspend / Hibernate / Close buttons. This is both a user feature and the primary debugging tool for the governor itself; the panel reads and writes the same scheduler, no side channel.

As built in P1, the panel sits in Settings → Memory under the threshold fields, and the command palette's "Running modules" opens it. Native modules share the shell process, so the panel shows OmniDeck's total against both caps rather than a per-module figure. Since P2, each open web app's row shows its own figure, and a "Shared web processes" row after the modules shows the shared ones. That row has no buttons, because the governor never demotes it. The pin toggle is labelled "Keep running", so it is not confused with pinning a module to the sidebar, and it is stored in `settings.json` (`memory.keepRunning`) next to the thresholds.

## 6. Testing

The scheduler is pure logic over an injected clock and an injected memory sampler — unit-test every transition, cap scenario, and pinning rule in `OmniDeck.Tests` without touching real WebView2.

**Resource soak, P2** (2 November 2026, Release build, one 2560×1440 monitor at 100 %):

- Idle on the library page, the shell held 103 MB of private commit, 32 GDI and 36 USER objects and 735 handles.
- 21 pop-out round trips (Clock into a window of its own and back) left the GDI and USER objects and handles flat after the first one: 39, 45 and 762.
- 21 page full-screen round trips on Discord's sign-in page also left them flat: 40, 49 and 798.
- With that page open, the seven WebView2 processes held 508 MB. The first full screen raised them to about 725 MB, mostly the GPU process's surfaces for the larger window, and they stayed there.

A signed-in day of use is the P2 exit's dogfooding ([ROADMAP](ROADMAP.md) §1).
