using Microsoft.Web.WebView2.Core;

namespace OmniDeck.Web.Apps;

/// <summary>A permission request waiting for the user's answer in a web app's view.</summary>
internal sealed class OdPermissionQuestion(CoreWebView2PermissionKind kind)
{
    public CoreWebView2PermissionKind Kind { get; } = kind;

    /// <summary>True when allowed; false when blocked, or when the view closed first.</summary>
    public TaskCompletionSource<bool> Answer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
