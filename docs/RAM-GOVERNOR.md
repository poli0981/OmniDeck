# OmniDeck — RAM Governor

The governor is the feature that makes "many apps in one" viable. It moves every module down a ladder of states the longer it goes unused, and enforces a total memory ceiling.

## 1. State ladder

| State | Entered when | Native module | Web app | Typical web-app RAM |
|---|---|---|---|---|
| Active | visible / interacted | full UI | full WebView2 | 300–700 MB |
| Idle | hidden < `IdleAfter` | keep view, stop animations/timers | hidden, still live | unchanged |
| Suspended | hidden ≥ `IdleAfter` | `SuspendAsync`: drop caches, charts, bitmaps; GC follows | `TrySuspendAsync` + `MemoryUsageTargetLevel.Low` | tens of MB |
| Hibernated | hidden ≥ `HibernateAfter`, or cap pressure | `HibernateAsync`: view + everything released, tiny state blob kept | `Dispose()`; only `{url}` kept, session lives in profile cookies | ≈ 0 |
| Closed | user closes | released | released | 0 |

Reactivation from Suspended is instant; from Hibernated it is a cold open (view rebuild / page reload) — acceptable because the profile keeps the user signed in.

## 2. Defaults (all configurable in Settings → Memory)

| Setting | Default |
|---|---|
| `IdleAfter` | 5 min hidden |
| `HibernateAfter` | 30 min hidden |
| `SoftCapBytes` | 4.5 GiB |
| `HardCapBytes` | 6.0 GiB |
| `PollInterval` | 10 s |

## 3. Pinning

A module is **pinned** (governor must not demote it below its current state) when any of:

- `CanSuspendAsync()` returns false (media player during playback);
- the web app's `IsDocumentPlayingAudio` is true **and** its catalog entry has `audioPin: true` (Spotify, YouTube);
- the user pinned it manually in the task-manager panel.

Pinned modules are skipped by cap enforcement; if the hard cap is exceeded purely by pinned modules, the governor toasts a warning instead of killing audio.

## 4. Cap enforcement

Total footprint = shell process + Σ all WebView2 processes (`CoreWebView2Environment.GetProcessInfos()` → PID), sampled every `PollInterval`.

**Metric — chosen by P0 spike #3.** Summing `Process.WorkingSet64` over-counts: Chromium processes share DLL and shared-memory pages, and each process reports them again. Working set also drops whenever pages are trimmed (for example by `MemoryUsageTargetLevel.Low`), so a working-set cap would reward exactly the page shuffling the honesty note below rejects. The spike compares private commit (`Process.PrivateMemorySize64`) with private working set and fixes one metric; the caps, the toasts, and the task-manager panel all use it.

- **> SoftCap:** demote the least-recently-used unpinned module one step down the ladder; repeat next tick if still above.
- **> HardCap:** force-hibernate LRU unpinned modules until below, then raise one toast summarizing what was hibernated.

Ordering hint: `RamClass.Heavy` modules (web apps, media, stocks charts) are preferred victims over `Light` ones at equal recency.

Honesty note: for in-process native modules the real win comes from modules *actually releasing* resources in `SuspendAsync`/`HibernateAsync`. The governor performs a `GC.Collect` + LOH compaction after demotions, and deliberately does **not** call `EmptyWorkingSet` tricks — those only shuffle pages to standby and fake the numbers.

## 5. Task-manager panel

Settings → Memory → "Running modules": live table of every module with state, memory estimate (the governor metric), last-used time, pin toggle, and Suspend / Hibernate / Close buttons. This is both a user feature and the primary debugging tool for the governor itself; the panel reads and writes the same scheduler, no side channel.

## 6. Testing

The scheduler is pure logic over an injected clock and an injected memory sampler — unit-test every transition, cap scenario, and pinning rule in `OmniDeck.Tests` without touching real WebView2.
