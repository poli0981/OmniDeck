using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Markup;
using Serilog;

namespace OmniDeck.Core.Localization;

/// <summary>
/// The live source of UI strings. XAML binds to this singleton's indexer through
/// <see cref="OdLocExtension"/>, so <see cref="SetCulture"/> updates every bound string without a
/// restart; code reads <see cref="Get"/> and re-reads on <see cref="CultureChanged"/>. Each assembly
/// with UI strings (the shell, the modules) adds its resources once with <see cref="Register"/>.
/// </summary>
public sealed class OdLocalization : INotifyPropertyChanged
{
    // What FollowCulture registered: each target held weakly, with the method to call on it.
    private readonly List<(WeakReference Target, MethodInfo Method)> _followers = [];
    private ResourceManager[] _resources = [];

    private OdLocalization()
    {
    }

    public event EventHandler? CultureChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The instance XAML binds to.</summary>
    public static OdLocalization Instance { get; } = new();

    /// <summary>The UI language in effect.</summary>
    public CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

    /// <summary>The Windows display language, captured before any <see cref="SetCulture"/>: what "follow Windows" means.</summary>
    public CultureInfo SystemCulture { get; } = CultureInfo.CurrentUICulture;

    /// <summary>The string for <paramref name="key"/> in the current language; the key itself when no resources have it.</summary>
    public string this[string key] => Array.ConvertAll(_resources, resources => resources.GetString(key, Culture))
        .FirstOrDefault(text => text is not null) ?? key;

    public static string Get(string key) => Instance[key];

    /// <summary>
    /// Calls <paramref name="handler"/> after each change of language, holding its target weakly, so a
    /// view model that outlives no one needs no unsubscribing. <paramref name="handler"/> must be a
    /// method of that view model: nothing else would keep a lambda's closure alive. A view model that
    /// lets go of what its texts come from, when it is disposed, calls <see cref="UnfollowCulture"/>.
    /// One follower that fails keeps its old texts; the others still follow.
    /// </summary>
    public static void FollowCulture(EventHandler<EventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (handler.Target is null)
        {
            throw new ArgumentException("Only an instance method can follow the culture: it is held by its target.", nameof(handler));
        }

        lock (Instance._followers)
        {
            Instance._followers.Add((new WeakReference(handler.Target), handler.Method));
        }
    }

    /// <summary>Stops calling <paramref name="handler"/> on a change of language.</summary>
    public static void UnfollowCulture(EventHandler<EventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (Instance._followers)
        {
            Instance._followers.RemoveAll(follower => !follower.Target.IsAlive
                || (ReferenceEquals(follower.Target.Target, handler.Target) && follower.Method == handler.Method));
        }
    }

    /// <summary>Fills a format string; numbers and dates follow the regional format, not the UI language.</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Instance[key], args);

    /// <summary>The culture a <c>Language</c> setting names; empty or unknown names follow Windows.</summary>
    public CultureInfo ResolveCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SystemCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(name.Trim(), predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return SystemCulture;
        }
    }

    /// <summary>Adds an assembly's string table; earlier tables win when two share a key.</summary>
    public void Register(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (!_resources.Contains(resources))
        {
            _resources = [.. _resources, resources];
        }
    }

    /// <summary>
    /// Switches the UI language and refreshes every bound string. Only the UI culture changes:
    /// number and date formats keep following the user's regional settings.
    /// </summary>
    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (string.Equals(culture.Name, Culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        TagOpenWindows();

        // "Item[]" tells WPF to re-read every indexer binding on this source.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        CultureChanged?.Invoke(this, EventArgs.Empty);
        NotifyFollowers();
    }

    /// <summary>
    /// Tags every window, open now or later, with the UI language (xml:lang): WPF picks Han glyph
    /// shapes from it, so Japanese text is not drawn with Chinese glyphs.
    /// </summary>
    public void TrackWindowLanguage()
    {
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                {
                    window.Language = XmlLanguage.GetLanguage(Culture.IetfLanguageTag);
                }
            }));
        TagOpenWindows();
    }

    // Each follower is called on its own: one that throws (a view model whose database is gone, say)
    // is logged and skipped, and the others still read their texts again.
    private void NotifyFollowers()
    {
        (object? Target, MethodInfo Method)[] followers;
        lock (_followers)
        {
            _followers.RemoveAll(follower => !follower.Target.IsAlive);
            followers = [.. _followers.Select(follower => (follower.Target.Target, follower.Method))];
        }

        foreach (var (target, method) in followers)
        {
            if (target is null)
            {
                continue;
            }

            try
            {
                method.Invoke(target, [this, EventArgs.Empty]);
            }
            catch (TargetInvocationException ex)
            {
                Log.ForContext<OdLocalization>().Warning(ex, "{Follower} could not follow the change of language", target.GetType().Name);
            }
        }
    }

    private void TagOpenWindows()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var language = XmlLanguage.GetLanguage(Culture.IetfLanguageTag);
        foreach (Window window in app.Windows)
        {
            window.Language = language;
        }
    }
}
