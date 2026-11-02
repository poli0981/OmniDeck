namespace OmniDeck.Modules.Media;

/// <summary>Which libVLC messages reach the app log: not the three harmless ones docs/spikes/06-libvlcsharp.md lists.</summary>
internal static class OdVlcLog
{
    private static readonly string[] Noise = ["option quiet does not exist", "SetThumbNailClip failed", "not enough decoding slices"];

    public static bool Keep(string message) => !Noise.Any(noise => message.Contains(noise, StringComparison.OrdinalIgnoreCase));
}
