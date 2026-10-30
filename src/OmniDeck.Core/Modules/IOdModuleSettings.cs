using System.Text.Json.Serialization.Metadata;

namespace OmniDeck.Core.Modules;

/// <summary>One JSON document per module, kept in the <c>module_settings</c> table.</summary>
public interface IOdModuleSettings
{
    /// <summary>The stored document, or <see langword="null"/> if there is none or it no longer parses.</summary>
    T? Get<T>(JsonTypeInfo<T> typeInfo)
        where T : class;

    void Set<T>(T value, JsonTypeInfo<T> typeInfo);
}
