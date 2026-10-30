using Microsoft.Win32;

namespace OmniDeck.App.Settings;

/// <summary>
/// <see cref="IOdStartupRegistration"/> through the per-user Run key: no admin rights, and removed
/// with the value. The command starts OmniDeck with <see cref="MinimizedArgument"/>, straight to the tray.
/// </summary>
public sealed class OdRunKeyStartup(string keyPath, string valueName, string executable) : IOdStartupRegistration
{
    public const string MinimizedArgument = "--minimized";

    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The registration for this process's executable under the real Run key.</summary>
    public OdRunKeyStartup()
        : this(RunKey, "OmniDeck", Environment.ProcessPath ?? "OmniDeck.exe")
    {
    }

    public string Command => $"\"{executable}\" {MinimizedArgument}";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return string.Equals(key?.GetValue(valueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        if (enabled)
        {
            key.SetValue(valueName, Command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
