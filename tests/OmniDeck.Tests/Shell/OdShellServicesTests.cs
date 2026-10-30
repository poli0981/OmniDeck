using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using OmniDeck.App.Shell;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace OmniDeck.Tests.Shell;

public sealed class OdShellServicesTests
{
    [Theory]
    [InlineData("Clock24", SymbolRegular.Clock24)]
    [InlineData("NoSuchIcon", SymbolRegular.Apps24)]
    [InlineData("12345", SymbolRegular.Apps24)]
    [InlineData(null, SymbolRegular.Apps24)]
    public void SymbolNames_FallBackToTheAppGlyph(string? name, SymbolRegular expected)
    {
        var converter = new OdSymbolConverter();

        Assert.Equal(expected, OdSymbolConverter.Parse(name));
        Assert.Equal(expected, converter.Convert(name, typeof(SymbolRegular), null, CultureInfo.InvariantCulture));
        Assert.Same(Binding.DoNothing, converter.ConvertBack(expected, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TrayNotifier_OnlyLogsUntilTheIconIsAttached()
    {
        var notifier = new OdTrayNotifier(Logger.None);
        var shown = new List<string>();

        notifier.Notify("Timer", "Before the icon");
        notifier.Attach((title, message) => shown.Add(title + ": " + message));
        notifier.Notify("Timer", "Done");

        Assert.Equal(new[] { "Timer: Done" }, shown, StringComparer.Ordinal);
    }

    [Fact]
    public void Lifetime_RemembersThatTheUserQuit()
    {
        var lifetime = new OdAppLifetime();

        Assert.False(lifetime.IsQuitting);
        lifetime.Quit();

        Assert.True(lifetime.IsQuitting);
    }

    [Fact]
    public void Dialogs_NeedAHost_AndCountOpenDialogs() => OdSta.Run(async () =>
    {
        var service = new OdFakeDialogService();
        var dialogs = new OdDialogs(service);
        Assert.False(dialogs.CanShow);

        var host = new ContentDialogHost();
        dialogs.SetHost(host);
        var showing = dialogs.ShowAsync(new ContentDialog());

        Assert.Same(host, service.Host);
        Assert.True(dialogs.IsOpen);
        Assert.False(dialogs.CanShow);
        service.Close(ContentDialogResult.Primary);
        Assert.Equal(ContentDialogResult.Primary, await showing);
        Assert.False(dialogs.IsOpen);

        // No visible main window in a test run: errors fall back to a message box, never a dialog.
        Assert.False(dialogs.CanShow);
    });

    private sealed class OdFakeDialogService : IContentDialogService
    {
        private TaskCompletionSource<ContentDialogResult> _result = new();

        public ContentDialogHost? Host { get; private set; }

        public void Close(ContentDialogResult result) => _result.SetResult(result);

        public void SetDialogHost(ContentPresenter dialogHost) => throw new NotSupportedException();

        public void SetDialogHost(ContentDialogHost dialogHost) => Host = dialogHost;

        public void SetContentPresenter(ContentPresenter contentPresenter) => throw new NotSupportedException();

        public ContentPresenter? GetContentPresenter() => null;

        public ContentPresenter? GetDialogHost() => null;

        public ContentDialogHost? GetDialogHostEx() => Host;

        public Task<ContentDialogResult> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken)
        {
            _result = new TaskCompletionSource<ContentDialogResult>();
            return _result.Task;
        }
    }
}
