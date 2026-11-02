using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace OmniDeck.Tests.TestSupport;

/// <summary>A logger that keeps every event written to it, for tests that check what was logged.</summary>
public sealed class OdLogCapture : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public OdLogCapture() => Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(this).CreateLogger();

    public ILogger Logger { get; }

    public IReadOnlyList<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Add(logEvent);
}
