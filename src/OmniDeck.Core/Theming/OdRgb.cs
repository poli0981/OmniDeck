using System.Runtime.InteropServices;

namespace OmniDeck.Core.Theming;

/// <summary>An opaque color, as the eye sees it on screen.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct OdRgb(byte R, byte G, byte B)
{
    /// <summary>The color of <c>#RRGGBB</c> or <c>#AARRGGBB</c> written as a number, alpha ignored.</summary>
    public static OdRgb FromArgb(uint argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}
