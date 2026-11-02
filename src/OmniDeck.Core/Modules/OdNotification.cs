namespace OmniDeck.Core.Modules;

/// <summary>A Windows notification under OmniDeck's name, and what clicking it opens.</summary>
/// <param name="Title">The first, bold line.</param>
/// <param name="Message">The text below it.</param>
/// <param name="Target">
/// What clicking the notification opens: the id of the module that raised it, or null to just show
/// the window. A module's notifications get its id unless they name something else.
/// </param>
/// <param name="Clicked">Run after the target opens, when the notification is clicked: a web app tells its page.</param>
public sealed record OdNotification(string Title, string Message, string? Target = null, Action? Clicked = null);
