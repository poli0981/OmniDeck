using OmniDeck.App.Diagnostics;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;
using Serilog.Events;

namespace OmniDeck.Tests.Diagnostics;

public sealed class OdDiagnosticsTests
{
    [Fact]
    public void Logger_WritesDailyFilesUnderTheDataRoot()
    {
        using var temp = new OdTempDirectory();
        var root = new OdDataRoot(temp.Path);
        root.EnsureCreated();

        using (var logger = (Logger)OdLogging.CreateLogger(root))
        {
            logger.Information("hello {Name}", "OmniDeck");
        }

        var file = Assert.Single(Directory.GetFiles(root.LogsDirectory, "omnideck-*.log"));
        Assert.Contains("[INF]", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Contains("hello OmniDeck", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void UiException_IsLoggedAndShownWithItsSupportCode()
    {
        var sink = new ListSink();
        var presenter = new RecordingPresenter();
        var handler = new OdCrashHandler(new LoggerConfigurationFor(sink).Logger, presenter, TimeProvider.System);

        var code = handler.HandleUiException(new InvalidOperationException("boom"));

        var shown = Assert.Single(presenter.Messages);
        Assert.Contains(code, shown, StringComparison.Ordinal);
        Assert.Contains("boom", shown, StringComparison.Ordinal);
        Assert.Equal(LogEventLevel.Error, Assert.Single(sink.Events).Level);
    }

    [Fact]
    public void AFailureWhileShowing_IsLoggedButNotStacked()
    {
        var presenter = new RecordingPresenter();
        var sink = new ListSink();
        var handler = new OdCrashHandler(new LoggerConfigurationFor(sink).Logger, presenter, TimeProvider.System);
        presenter.OnShow = () => handler.HandleUiException(new InvalidOperationException("inside the dialog"));

        handler.HandleUiException(new InvalidOperationException("first"));

        Assert.Single(presenter.Messages);
        Assert.Equal(2, sink.Events.Count);
    }

    [Fact]
    public void FatalAndUnobservedExceptions_AreLogged()
    {
        var sink = new ListSink();
        var handler = new OdCrashHandler(new LoggerConfigurationFor(sink).Logger, new RecordingPresenter(), TimeProvider.System);

        handler.HandleUnobserved(new TimeoutException("late"));
        handler.HandleFatal(new InvalidOperationException("fatal"), terminating: false);

        Assert.Equal(new[] { LogEventLevel.Error, LogEventLevel.Fatal }, sink.Events.Select(e => e.Level));
    }

    private sealed class RecordingPresenter : IOdErrorPresenter
    {
        public List<string> Messages { get; } = [];

        public Action? OnShow { get; set; }

        public void Show(string message)
        {
            Messages.Add(message);
            OnShow?.Invoke();
        }
    }

    private sealed class ListSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class LoggerConfigurationFor(ILogEventSink sink)
    {
        public Serilog.ILogger Logger { get; } = new Serilog.LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
    }
}
