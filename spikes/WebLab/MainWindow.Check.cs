namespace WebLab;

/// <summary>
/// `--check-signed-in`: reopen the catalog apps on their saved profiles and ask each page whether it
/// is signed in, to tell whether sign-ins survived the last shutdown (spike #2). Gmail then reloads
/// with CDP capture on, to check that a signed-in app still loads with Runtime.enable (spike #4).
/// `--ladder=gmail,...`: only the governor ladder, for the listed apps.
/// </summary>
public partial class MainWindow
{
    // Each expression returns "in" or "out". It only reads the page's own state.
    private static readonly Dictionary<string, string> SignedInProbes = new()
    {
        ["gmail"] = "location.host === 'mail.google.com' ? 'in' : 'out'",
        ["youtube"] = "document.querySelector('#avatar-btn') ? 'in' : 'out'",
        ["spotify"] = "document.querySelector('[data-testid=\"login-button\"]') ? 'out' : 'in'",
        ["facebook"] = "document.querySelector('input[name=\"email\"]') ? 'out' : 'in'",
        ["discord"] = "location.pathname.startsWith('/channels') ? 'in' : 'out'",
    };

    private async Task RunSignedInCheckAsync()
    {
        var targets = ((List<Target>)Targets.ItemsSource).Where(t => t.Entry is not null && SignedInProbes.ContainsKey(t.Entry.Id)).ToList();
        try
        {
            foreach (var target in targets)
            {
                var slot = await OpenAsync(target);
                await Task.Delay(TimeSpan.FromSeconds(12));
                await LogSignedInAsync(slot);
            }

            var gmail = _slots.First(s => s.Id == "gmail");
            ShowOnly(gmail);
            await SetCaptureAsync(gmail, runtime: true, log: true);
            var done = new TaskCompletionSource<bool>();
            gmail.View!.CoreWebView2.NavigationCompleted += (_, e) => done.TrySetResult(e.IsSuccess);
            gmail.View.CoreWebView2.Reload();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await Task.Delay(TimeSpan.FromSeconds(10));
            Log("[gmail] after a reload with capture on:");
            await LogSignedInAsync(gmail);
            Log($"[gmail] ring buffer holds {gmail.ConsoleRing.Count} entries");
        }
        catch (Exception ex)
        {
            Log($"CHECK FAILED: {ex}");
        }
        finally
        {
            foreach (var slot in _slots.ToList())
            {
                Hibernate(slot);
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
            Close();
        }
    }

    private async Task LogSignedInAsync(Slot slot)
    {
        var id = slot.Entry!.Id;
        var result = await slot.View!.CoreWebView2.ExecuteScriptAsync(SignedInProbes[id]);
        Log($"SIGNED-IN {id}: {result.Trim('"')} at {Catalog.Redact(slot.View.CoreWebView2.Source)}");
    }

    private async Task RunLaddersOnlyAsync(string ids)
    {
        var targets = (List<Target>)Targets.ItemsSource;
        var phase = TimeSpan.FromSeconds(int.TryParse(Arg("--phase"), out var s) && s > 0 ? s : 60);
        try
        {
            StartCsv();
            foreach (var id in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var slot = await OpenAsync(targets.First(t => t.Entry?.Id == id));
                await Task.Delay(TimeSpan.FromSeconds(12));
                await LogSignedInAsync(slot);
                await RunLadderAsync(slot, phase);
                await LogSignedInAsync(slot);
                Hibernate(slot);
                _slots.Remove(slot);
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception ex)
        {
            Log($"LADDER FAILED: {ex}");
        }
        finally
        {
            StopCsv();
            await Task.Delay(500);
            Close();
        }
    }
}
