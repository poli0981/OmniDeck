namespace WebLab;

/// <summary>
/// Cookie persistence across host shutdowns (spike #2 follow-up): sign-ins made in an interactive
/// session were gone after the host was closed, while cookies from a run that disposed every view
/// first survived.
///   `--cookie-set=NAME [--exit=clean]`: set a one-year cookie, wait past the cookie store's commit
///     interval, then either dispose every view and exit (clean) or wait to be closed from outside.
///   `--cookie-check`: list the cookies the probe page can see, then exit cleanly.
/// </summary>
public partial class MainWindow
{
    private async Task RunCookieProbeAsync()
    {
        var page = new Target("lab · cookie probe", $"https://{LabHost}/cookie.html", "lab", null);
        try
        {
            if (Arg("--cookie-set") is { } name)
            {
                var wait = WaitForMessageAsync("cookie");
                await OpenAsync(page with { Url = page.Url + "?set=" + Uri.EscapeDataString(name) });
                var result = await wait;
                Log($"COOKIE set '{name}'; before: \"{result.GetProperty("before").GetString()}\"");
                await Task.Delay(TimeSpan.FromSeconds(45)); // past the 30 s commit interval
                if (Arg("--exit") == "clean")
                {
                    foreach (var slot in _slots.ToList())
                    {
                        Hibernate(slot);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(3));
                    Log("COOKIE exit: clean (views disposed first)");
                    Close();
                }
                else
                {
                    Log("COOKIE ready to be closed from outside");
                }
            }
            else
            {
                var wait = WaitForMessageAsync("cookie");
                await OpenAsync(page);
                var result = await wait;
                Log($"COOKIE check: \"{result.GetProperty("before").GetString()}\"");
                foreach (var slot in _slots.ToList())
                {
                    Hibernate(slot);
                }

                await Task.Delay(TimeSpan.FromSeconds(3));
                Close();
            }
        }
        catch (Exception ex)
        {
            Log($"COOKIE FAILED: {ex}");
            Close();
        }
    }
}
