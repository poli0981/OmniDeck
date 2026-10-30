namespace WebLab;

/// <summary>One open web app: the lab's stand-in for a governed web module.</summary>
public sealed class Slot
{
    public required string Id { get; init; }

    public required string Profile { get; init; }

    public required string StartUrl { get; init; }

    public CatalogEntry? Entry { get; init; }

    public WebView2? View { get; set; }

    public string State { get; set; } = "Created";

    /// <summary>The only thing kept across hibernation (docs/RAM-GOVERNOR.md §1).</summary>
    public string? SavedUrl { get; set; }

    public bool Capturing { get; set; }

    public CoreWebView2DevToolsProtocolEventReceiver? ConsoleReceiver { get; set; }

    public CoreWebView2DevToolsProtocolEventReceiver? LogReceiver { get; set; }

    public Queue<string> ConsoleRing { get; } = new();

    public double AttributedMb { get; set; }

    public void AddConsole(string line)
    {
        // The SPEC's 200-entry ring buffer per app.
        if (ConsoleRing.Count == 200)
        {
            ConsoleRing.Dequeue();
        }

        ConsoleRing.Enqueue(line);
    }

    public override string ToString() => $"{Id} [{Profile}] {State} · {AttributedMb:F0} MB";
}
