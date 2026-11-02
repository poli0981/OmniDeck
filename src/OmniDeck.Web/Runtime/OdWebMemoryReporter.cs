using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using OmniDeck.Core.Governor;
using Serilog;

namespace OmniDeck.Web.Runtime;

/// <summary>
/// The web runner's part of OmniDeck's memory (docs/RAM-GOVERNOR.md §4): the private commit of every
/// WebView2 process. A renderer counts for the web app whose pages it hosts, found by each frame's main
/// frame (docs/spikes/03-memory.md), or by the page's host when only one app shows it; the browser,
/// GPU and utility processes, and pages no open app shows, count as shared.
/// </summary>
public sealed class OdWebMemoryReporter(OdWebRuntime runtime, ILogger log) : IOdMemoryReporter
{
    private readonly ILogger _log = log.ForContext<OdWebMemoryReporter>();

    public async Task<OdMemoryReport> ReportAsync()
    {
        if (runtime.Current is not { } environment)
        {
            return OdMemoryReport.None;
        }

        IReadOnlyList<CoreWebView2ProcessExtendedInfo> infos;
        try
        {
            infos = await environment.GetProcessExtendedInfosAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The browser process is restarting; the next pass measures again.
            _log.Debug(ex, "The WebView2 processes could not be listed");
            return OdMemoryReport.None;
        }

        var processes = infos.Select(info => new OdWebProcess(info.ProcessInfo.ProcessId, MainFrames(info.AssociatedFrameInfos))).ToList();
        return Attribute(processes, runtime.Pages(), PrivateCommit);
    }

    /// <summary>Splits the processes' private commit between the apps whose pages they host; the rest is shared.</summary>
    internal static OdMemoryReport Attribute(IReadOnlyList<OdWebProcess> processes, IReadOnlyList<OdWebPage> pages, Func<int, long> commit)
    {
        var modules = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            modules.TryAdd(page.ModuleId, 0);
        }

        var shared = 0L;
        foreach (var process in processes)
        {
            var bytes = commit(process.Id);
            string[] owners = [.. process.MainFrames.Select(frame => Owner(frame, pages)).OfType<string>().Distinct(StringComparer.Ordinal)];
            if (owners.Length == 0)
            {
                shared += bytes;
                continue;
            }

            // A renderer that hosts pages of several apps is split evenly between them.
            var share = bytes / owners.Length;
            foreach (var owner in owners)
            {
                modules[owner] += share;
            }

            shared += bytes - (share * owners.Length);
        }

        return new OdMemoryReport(shared, modules);
    }

    /// <summary>The app whose page <paramref name="frame"/> is: by its main frame, or else by host when only one app shows that host.</summary>
    private static string? Owner(OdWebFrame frame, IReadOnlyList<OdWebPage> pages)
    {
        if (frame.Id != 0 && pages.FirstOrDefault(page => page.MainFrameId == frame.Id) is { } shown)
        {
            return shown.ModuleId;
        }

        if (frame.Host.Length == 0)
        {
            return null;
        }

        string[] byHost = [.. pages.Where(page => string.Equals(page.Host, frame.Host, StringComparison.OrdinalIgnoreCase)).Select(page => page.ModuleId).Distinct(StringComparer.Ordinal)];
        return byHost.Length == 1 ? byHost[0] : null;
    }

    private static List<OdWebFrame> MainFrames(IReadOnlyList<CoreWebView2FrameInfo> frames)
    {
        var mains = new List<OdWebFrame>();
        foreach (var frame in frames)
        {
            var main = frame;
            while (main.ParentFrameInfo is { } parent)
            {
                main = parent;
            }

            if (!mains.Exists(known => known.Id == main.FrameId))
            {
                mains.Add(new OdWebFrame(main.FrameId, Uri.TryCreate(main.Source, UriKind.Absolute, out var source) ? source.Host : ""));
            }
        }

        return mains;
    }

    private static long PrivateCommit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.PrivateMemorySize64;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // It exited after the list was taken.
            return 0;
        }
    }
}
