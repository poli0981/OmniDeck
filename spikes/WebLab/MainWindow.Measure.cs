using System.Text.RegularExpressions;

namespace WebLab;

/// <summary>
/// `WebLab.exe --measure [--phase=60]`: spikes #3 and #4 on the profiles signed in by hand earlier.
/// A governor ladder per app (Discord, YouTube, Gmail), every catalog app open at once, then CDP
/// capture overhead and whether signed-in apps still load with Runtime.enable on.
/// </summary>
public partial class MainWindow
{
    private static readonly string[] LadderApps = ["discord", "youtube", "gmail"];

    private async Task RunMeasureAsync()
    {
        // Messenger is left out: messenger.com closed in April 2026.
        var targets = ((List<Target>)Targets.ItemsSource).Where(t => t.Entry is { Id: not "messenger" }).ToList();
        var phase = TimeSpan.FromSeconds(int.TryParse(Arg("--phase"), out var s) && s > 0 ? s : 60);
        Target App(string id) => targets.First(t => t.Entry!.Id == id);
        try
        {
            StartCsv();
            Mark("measure:shell-only");
            await Task.Delay(TimeSpan.FromSeconds(10));

            foreach (var id in LadderApps)
            {
                var slot = await OpenAsync(App(id));
                await RunLadderAsync(slot, phase);
                LogTitle(slot);
                Hibernate(slot);
                _slots.Remove(slot);
                Mark("measure:gap");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            // Every catalog app at once; the last one opened stays visible, the others are hidden.
            var all = new List<Slot>();
            foreach (var target in targets)
            {
                all.Add(await OpenAsync(target));
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            Mark("all:open");
            await Task.Delay(phase * 1.5);
            all.ForEach(LogTitle);
            foreach (var slot in all.Take(all.Count - 1))
            {
                await SuspendAsync(slot);
            }

            Mark("all:suspended");
            await Task.Delay(phase);
            all.ForEach(Hibernate);
            Mark("all:hibernated");
            await Task.Delay(phase / 2);
            all.ForEach(slot => _slots.Remove(slot));

            // CDP capture on signed-in apps: cost, and whether they still load with Runtime.enable on.
            var cdp = new List<Slot>();
            foreach (var id in new[] { "gmail", "discord", "spotify" })
            {
                cdp.Add(await OpenAsync(App(id)));
            }

            Mark("cdp:off");
            await Task.Delay(phase);
            foreach (var slot in cdp)
            {
                await SetCaptureAsync(slot, runtime: true, log: true);
            }

            Mark("cdp:on");
            await Task.Delay(phase);
            foreach (var slot in cdp)
            {
                var done = new TaskCompletionSource<bool>();
                slot.View!.CoreWebView2.NavigationCompleted += (_, e) => done.TrySetResult(e.IsSuccess);
                slot.View.CoreWebView2.Reload();
                Log($"[{slot.Id}] reload with capture on: {(await done.Task.WaitAsync(TimeSpan.FromSeconds(60)) ? "ok" : "failed")}");
            }

            Mark("cdp:on-reloaded");
            await Task.Delay(phase);
            foreach (var slot in cdp)
            {
                LogTitle(slot);
                Log($"[{slot.Id}] ring buffer holds {slot.ConsoleRing.Count} entries; last 10:");
                foreach (var line in slot.ConsoleRing.TakeLast(10))
                {
                    Log($"[{slot.Id}]   {RedactEmails(line)}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"MEASURE FAILED: {ex}");
        }
        finally
        {
            StopCsv();
            await Task.Delay(500);
            Close();
        }
    }

    // Titles show whether an app is still signed in; addresses in them stay out of the log.
    private void LogTitle(Slot slot)
    {
        try
        {
            Log($"[{slot.Id}] title \"{RedactEmails(slot.View?.CoreWebView2?.DocumentTitle ?? "")}\" at {Catalog.Redact(slot.View?.CoreWebView2?.Source ?? "")}");
        }
        catch (InvalidOperationException)
        {
            // The view is being torn down.
        }
    }

    private static string RedactEmails(string text) => Regex.Replace(text, @"[^\s@""']+@[^\s@""']+", "<email>");
}
