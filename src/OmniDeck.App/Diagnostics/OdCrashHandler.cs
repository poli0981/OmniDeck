using System.Globalization;
using System.Windows;
using Serilog;

namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Global failure handling (docs/ARCHITECTURE.md §6): every unhandled exception is logged; one on the
/// UI thread is shown with a support code that matches the log entry, and OmniDeck keeps running.
/// The bug-report flow (P2) hangs off the same dialog.
/// </summary>
internal sealed class OdCrashHandler
{
    private readonly ILogger _log;
    private readonly IOdErrorPresenter _presenter;
    private readonly TimeProvider _time;
    private int _showing;

    public OdCrashHandler(ILogger log, IOdErrorPresenter presenter, TimeProvider time)
    {
        _log = log.ForContext<OdCrashHandler>();
        _presenter = presenter;
        _time = time;
    }

    public void Hook(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            HandleUiException(e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleFatal(e.ExceptionObject as Exception, e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            HandleUnobserved(e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>A short code the user can quote; it is logged next to the exception.</summary>
    internal static string SupportCode(Exception exception, DateTimeOffset when)
    {
        // FNV-1a over the exception type and message: stable for the same failure, cheap, and free of
        // anything personal.
        var hash = 2166136261u;
        foreach (var c in exception.GetType().FullName + exception.Message)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{when:yyMMdd-HHmm}-{hash % 0x10000:X4}");
    }

    /// <summary>A UI-thread failure: logged, shown with its support code; the app keeps running.</summary>
    /// <returns>The support code.</returns>
    internal string HandleUiException(Exception exception)
    {
        var code = SupportCode(exception, _time.GetLocalNow());
        _log.Error(exception, "Unhandled UI exception, support code {SupportCode}", code);

        // A failure inside the dialog itself must not stack dialogs.
        if (Interlocked.Exchange(ref _showing, 1) == 0)
        {
            try
            {
                _presenter.Show($"Something went wrong, but OmniDeck is still running.\n\nSupport code: {code}\n\n{exception.Message}");
            }
            finally
            {
                Volatile.Write(ref _showing, 0);
            }
        }

        return code;
    }

    internal void HandleFatal(Exception? exception, bool terminating)
    {
        _log.Fatal(exception, "Unhandled exception (terminating: {Terminating})", terminating);
        Log.CloseAndFlush();
    }

    internal void HandleUnobserved(Exception exception) => _log.Error(exception, "Unobserved task exception");
}
