using System.Globalization;
using System.Windows;
using System.Windows.Threading;
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
    private int _showing;

    public OdCrashHandler(ILogger log)
    {
        _log = log.ForContext<OdCrashHandler>();
    }

    public void Hook(Application app)
    {
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            _log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (terminating: {Terminating})", e.IsTerminating);
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _log.Error(e.Exception, "Unobserved task exception");
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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var code = SupportCode(e.Exception, DateTimeOffset.Now);
        _log.Error(e.Exception, "Unhandled UI exception, support code {SupportCode}", code);
        e.Handled = true;

        // A failure inside the dialog itself must not stack dialogs.
        if (Interlocked.Exchange(ref _showing, 1) == 1)
        {
            return;
        }

        try
        {
            MessageBox.Show(
                $"Something went wrong, but OmniDeck is still running.\n\nSupport code: {code}\n\n{e.Exception.Message}",
                "OmniDeck",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            Volatile.Write(ref _showing, 0);
        }
    }
}
