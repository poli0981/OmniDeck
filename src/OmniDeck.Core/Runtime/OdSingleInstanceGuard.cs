using System.Runtime.InteropServices;

namespace OmniDeck.Core.Runtime;

/// <summary>
/// One OmniDeck per Windows session (docs/SPEC.md §2). The first instance owns a named mutex and
/// waits on a named event; a second instance sets the event, lets the first one take the foreground,
/// and exits.
/// </summary>
public sealed partial class OdSingleInstanceGuard : IDisposable
{
    private const int AsfwAny = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly ManualResetEvent _stop = new(initialState: false);
    private Thread? _listener;

    /// <summary>Claims the instance if no other OmniDeck holds it.</summary>
    /// <param name="nameSuffix">Isolates tests from each other and from a running OmniDeck.</param>
    public OdSingleInstanceGuard(string? nameSuffix = null)
    {
        var name = $@"Local\OmniDeck.SingleInstance{nameSuffix}";
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        IsPrimaryInstance = createdNew;
        _activate = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, name + ".Activate");
    }

    public bool IsPrimaryInstance { get; }

    /// <summary>From a second instance: asks the first one to show itself.</summary>
    public void SignalPrimaryInstance()
    {
        AllowSetForegroundWindow(AsfwAny);
        _activate.Set();
    }

    /// <summary>From the first instance: runs <paramref name="onActivate"/> whenever a second one starts.</summary>
    public void StartListening(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        if (!IsPrimaryInstance || _listener is not null)
        {
            return;
        }

        _listener = new Thread(() =>
        {
            WaitHandle[] handles = [_activate, _stop];
            while (WaitHandle.WaitAny(handles) == 0)
            {
                onActivate();
            }
        })
        {
            IsBackground = true,
            Name = "OmniDeck single-instance listener",
        };
        _listener.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _listener?.Join(TimeSpan.FromSeconds(2));
        if (IsPrimaryInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activate.Dispose();
        _stop.Dispose();
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
