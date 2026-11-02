using System.Globalization;
using System.Windows;
using OmniDeck.Core.Localization;
using Serilog;

namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Global failure handling (docs/ARCHITECTURE.md §6): every unhandled exception is logged; one on the
/// UI thread is shown with a support code that matches the log entry, and OmniDeck keeps running.
/// A failure that repeats is reported once per <see cref="RepeatWindow"/> and only counted in between:
/// a control stuck in a loop once threw the same exception thousands of times a second, and each one
/// was logged in full and wanted a dialog of its own. The bug-report flow (P2) hangs off the same dialog.
/// </summary>
internal sealed class OdCrashHandler
{
    /// <summary>How long a repeat of a reported failure is only counted.</summary>
    internal static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(1);

    private readonly ILogger _log;
    private readonly IOdErrorPresenter _presenter;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Dictionary<uint, Reported> _reported = [];
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
    internal static string SupportCode(Exception exception, DateTimeOffset when) =>
        string.Create(CultureInfo.InvariantCulture, $"{when:yyMMdd-HHmm}-{Fingerprint(exception) % 0x10000:X4}");

    /// <summary>A UI-thread failure: logged, shown with its support code; the app keeps running.</summary>
    /// <returns>The support code.</returns>
    internal string HandleUiException(Exception exception)
    {
        var now = _time.GetLocalNow();
        var code = SupportCode(exception, now);
        if (!ShouldReport(exception, now, out var repeats))
        {
            return code;
        }

        if (repeats > 0)
        {
            _log.Error(exception, "Unhandled UI exception, support code {SupportCode}; it repeated {Repeats} times since the last report", code, repeats);
        }
        else
        {
            _log.Error(exception, "Unhandled UI exception, support code {SupportCode}", code);
        }

        // A failure inside the dialog itself must not stack dialogs.
        if (Interlocked.Exchange(ref _showing, 1) == 0)
        {
            try
            {
                _presenter.Show(OdLocalization.Format("Error_UiMessage", code, exception.Message));
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

    internal void HandleUnobserved(Exception exception)
    {
        if (!ShouldReport(exception, _time.GetLocalNow(), out var repeats))
        {
            return;
        }

        if (repeats > 0)
        {
            _log.Error(exception, "Unobserved task exception; it repeated {Repeats} times since the last report", repeats);
        }
        else
        {
            _log.Error(exception, "Unobserved task exception");
        }
    }

    // FNV-1a over the exception type and message: stable for the same failure, cheap, and free of
    // anything personal.
    private static uint Fingerprint(Exception exception)
    {
        var hash = 2166136261u;
        foreach (var c in exception.GetType().FullName + exception.Message)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    /// <summary>
    /// Whether to report <paramref name="exception"/> now. A repeat within <see cref="RepeatWindow"/> of
    /// its last report is only counted; <paramref name="repeats"/> is the count since that report.
    /// </summary>
    private bool ShouldReport(Exception exception, DateTimeOffset now, out int repeats)
    {
        var fingerprint = Fingerprint(exception);
        lock (_gate)
        {
            if (_reported.TryGetValue(fingerprint, out var last) && now - last.At < RepeatWindow)
            {
                _reported[fingerprint] = last with { Repeats = last.Repeats + 1 };
                repeats = 0;
                return false;
            }

            repeats = last?.Repeats ?? 0;
            _reported[fingerprint] = new Reported(now, 0);
            return true;
        }
    }

    private sealed record Reported(DateTimeOffset At, int Repeats);
}
