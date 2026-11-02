using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using OmniDeck.Core.Storage;
using Serilog;

namespace OmniDeck.Web.Runtime;

/// <summary>
/// The one WebView2 environment the web apps share (docs/ARCHITECTURE.md §3), created when the first
/// one opens, with its user data in the data root's <c>WebView2</c> folder; each catalog profile is a
/// WebView2 profile in it. It also follows the shared browser process, which writes cookies out as
/// it exits: quitting waits for that, and so does opening an app while it is exiting, since a
/// controller created then stalls for seconds (docs/spikes/03-memory.md). It also knows which app
/// each open browser belongs to, for the memory figures. Used on the UI thread.
/// </summary>
public sealed class OdWebRuntime(OdDataRoot dataRoot, OdWebOptions options, ILogger log)
{
    /// <summary>How long to wait for the browser process to exit.</summary>
    public static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(3);

    private readonly ILogger _log = log.ForContext<OdWebRuntime>();
    private readonly List<(string ModuleId, CoreWebView2 Browser)> _browsers = [];
    private Task<CoreWebView2Environment>? _environment;
    private TaskCompletionSource? _exiting;
    private int _open;

    /// <summary>The environment once a web app has opened; null before that.</summary>
    public CoreWebView2Environment? Current { get; private set; }

    /// <summary>The installed WebView2 Runtime's version; null when it is missing.</summary>
    public static string? InstalledVersion()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The environment, for a browser about to open; pair every call with <see cref="Close"/>.</summary>
    public async Task<CoreWebView2Environment> OpenAsync()
    {
        if (_exiting is { } exiting)
        {
            await Task.WhenAny(exiting.Task, Task.Delay(ExitWait));
        }

        _open++;
        try
        {
            return await (_environment ??= CreateAsync());
        }
        catch
        {
            _open--;
            _environment = null;
            throw;
        }
    }

    /// <summary>Called as a browser closes; after the last one, the shared browser process exits.</summary>
    public void Close()
    {
        _open = Math.Max(0, _open - 1);
        if (_open == 0 && _environment is { IsCompletedSuccessfully: true })
        {
            _exiting ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Waits, up to <see cref="ExitWait"/>, for the browser process to exit after the last browser closed.</summary>
    public Task WaitForExitAsync() => _exiting is { } exiting ? Task.WhenAny(exiting.Task, Task.Delay(ExitWait)) : Task.CompletedTask;

    /// <summary>Counts <paramref name="browser"/>'s pages for <paramref name="moduleId"/> until <see cref="Forget"/>.</summary>
    public void Track(string moduleId, CoreWebView2 browser) => _browsers.Add((moduleId, browser));

    /// <summary>Stops counting a browser that is closing.</summary>
    public void Forget(CoreWebView2 browser) => _browsers.RemoveAll(entry => ReferenceEquals(entry.Browser, browser));

    /// <summary>The page each open browser shows: its app, its main frame and its host.</summary>
    internal List<OdWebPage> Pages()
    {
        var pages = new List<OdWebPage>(_browsers.Count);
        foreach (var (moduleId, browser) in _browsers)
        {
            try
            {
                pages.Add(new OdWebPage(moduleId, browser.FrameId, Uri.TryCreate(browser.Source, UriKind.Absolute, out var source) ? source.Host : ""));
            }
            catch (Exception ex) when (ex is InvalidOperationException or COMException)
            {
                // The browser is closing; its processes count as shared until they exit.
            }
        }

        return pages;
    }

    /// <summary>
    /// Deletes everything the catalog profile <paramref name="profile"/> keeps: cookies, so every
    /// sign-in, site storage, the cache and the history. Close its apps first. A hidden browser in
    /// <paramref name="window"/> does the work.
    /// </summary>
    public async Task ClearProfileAsync(string profile, nint window)
    {
        var environment = await OpenAsync();
        try
        {
            var controller = await environment.CreateCoreWebView2ControllerAsync(window, ControllerOptions(environment, profile));
            try
            {
                controller.IsVisible = false;
                await controller.CoreWebView2.Profile.ClearBrowsingDataAsync();
                _log.Information("Cleared the data of web profile {Profile}", profile);
            }
            finally
            {
                controller.Close();
            }
        }
        finally
        {
            Close();
        }
    }

    /// <summary>Options that open a browser on the catalog profile <paramref name="profile"/>.</summary>
    public static CoreWebView2ControllerOptions ControllerOptions(CoreWebView2Environment environment, string profile)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var controller = environment.CreateCoreWebView2ControllerOptions();
        controller.ProfileName = profile;
        return controller;
    }

    private async Task<CoreWebView2Environment> CreateAsync()
    {
        var environmentOptions = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = options.ChromiumLogging ? "--enable-logging --v=0" : null,
        };
        var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: dataRoot.WebView2Directory, environmentOptions);
        environment.BrowserProcessExited += OnBrowserProcessExited;
        Current = environment;
        _log.Information("WebView2 {Version} ready (Chromium logging {Logging})", environment.BrowserVersionString, options.ChromiumLogging);
        return environment;
    }

    private void OnBrowserProcessExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs e)
    {
        _log.Information("The WebView2 browser process exited ({Kind})", e.BrowserProcessExitKind);
        _exiting?.TrySetResult();
        _exiting = null;
    }
}
