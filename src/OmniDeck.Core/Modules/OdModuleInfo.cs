namespace OmniDeck.Core.Modules;

/// <summary>What the shell shows about a module.</summary>
/// <param name="Id">Stable identifier, also the settings key: <c>[a-z0-9-]</c>.</param>
/// <param name="NameKey">The resource key of the module's localized name.</param>
/// <param name="Icon">A WPF-UI <c>SymbolRegular</c> name such as <c>Clock24</c>; Core stays free of WPF-UI.</param>
/// <param name="CategoryKey">The resource key of the localized category the library shows under the name.</param>
/// <param name="RamClass">The governor's priority hint.</param>
public sealed record OdModuleInfo(string Id, string NameKey, string Icon, string CategoryKey, OdRamClass RamClass);
