using Microsoft.Web.WebView2.Core;
using OmniDeck.Web.Apps;

namespace OmniDeck.Tests.Web;

public sealed class OdWebPermissionsTests
{
    [Theory]
    [InlineData(CoreWebView2PermissionKind.Notifications, true, null, CoreWebView2PermissionState.Allow)]
    [InlineData(CoreWebView2PermissionKind.Notifications, false, true, CoreWebView2PermissionState.Deny)]
    [InlineData(CoreWebView2PermissionKind.Microphone, false, true, CoreWebView2PermissionState.Allow)]
    [InlineData(CoreWebView2PermissionKind.Camera, true, false, CoreWebView2PermissionState.Deny)]
    [InlineData(CoreWebView2PermissionKind.Autoplay, false, null, CoreWebView2PermissionState.Default)]
    [InlineData(CoreWebView2PermissionKind.PersistentStorage, false, false, CoreWebView2PermissionState.Default)]
    [InlineData(CoreWebView2PermissionKind.UnknownPermission, true, true, CoreWebView2PermissionState.Deny)]
    public void Requests_AreAnswered_WithoutAsking(CoreWebView2PermissionKind kind, bool notificationsOn, bool? remembered, CoreWebView2PermissionState answer) =>
        Assert.Equal(answer, OdWebPermissions.Decide(kind, notificationsOn, remembered));

    [Fact]
    public void KindsThatReachDevicesOrData_AreAsked_UntilAnswered()
    {
        Assert.All(OdWebPermissions.Asked, kind => Assert.Null(OdWebPermissions.Decide(kind, notificationsOn: true, remembered: null)));
        Assert.Equal("Discord wants to use your microphone.", OdWebPermissions.Question(CoreWebView2PermissionKind.Microphone, "Discord"));
        Assert.Equal("Location", OdWebPermissions.Name("Geolocation"));
        Assert.Equal("Notifications", OdWebPermissions.Name("Notifications"));
    }

    [Fact]
    public void ANotification_NamesTheApp_ThenThePagesTitle()
    {
        var clicks = 0;
        var shown = OdWebNotifications.From("Discord", "  Alice\n(#general) ", "hello there", () => clicks++);

        Assert.Equal("Discord: Alice (#general)", shown.Title);
        Assert.Equal("hello there", shown.Message);
        Assert.Null(shown.Target);
        shown.Clicked?.Invoke();
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void WithoutABody_TheTitleMovesDown_AndWithNothing_ItStillSaysSomething()
    {
        var titleOnly = OdWebNotifications.From("Gmail", "New mail", " ", () => { });
        var empty = OdWebNotifications.From("Gmail", null, null, () => { });

        Assert.Equal(("Gmail", "New mail"), (titleOnly.Title, titleOnly.Message));
        Assert.Equal(("Gmail", "New notification"), (empty.Title, empty.Message));
    }

    [Fact]
    public void LongText_IsCut_ToWhatWindowsShows()
    {
        var shown = OdWebNotifications.From("Discord", new string('t', 100), new string('b', 300), () => { });
        var emoji = OdWebNotifications.From("Discord", "x", new string('b', 253) + "\U0001F600\U0001F600", () => { });

        Assert.Equal(OdWebNotifications.MostTitle, shown.Title.Length);
        Assert.EndsWith("\u2026", shown.Title, StringComparison.Ordinal);
        Assert.Equal(OdWebNotifications.MostMessage, shown.Message.Length);
        Assert.Equal(new string('b', 253) + "\u2026", emoji.Message);
    }
}
