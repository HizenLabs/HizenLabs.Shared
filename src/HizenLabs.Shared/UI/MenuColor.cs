using System.Collections.Generic;
using System.Globalization;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Color normalization for CUI: the client only parses "r g b a" (floats 0-1, space-separated),
/// so hex input ("#RRGGBB", "RRGGBBAA", with or without '#') is converted server-side. Results
/// are cached per distinct input string - a UI palette is a handful of colors, so steady-state
/// lookups allocate nothing. Strings already containing a space pass through untouched.
/// </summary>
public static class MenuColor
{
    private static readonly Dictionary<string, string> _cache = new();

    public static string Normalize(string color)
    {
        if (string.IsNullOrEmpty(color) || color.IndexOf(' ') >= 0)
            return color;

        if (_cache.TryGetValue(color, out var parsed))
            return parsed;

        parsed = ParseHex(color) ?? color;
        _cache[color] = parsed;
        return parsed;
    }

    /// <summary>"RRGGBB"/"RRGGBBAA" (optionally '#'-prefixed) to "r g b a", or null if not hex.</summary>
    private static string ParseHex(string color)
    {
        var hex = color[0] == '#' ? color.Substring(1) : color;
        if (hex.Length != 6 && hex.Length != 8)
            return null;

        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return null;

        if (hex.Length == 6)
            value = (value << 8) | 0xFF; // no alpha digits: opaque

        var r = (byte)(value >> 24);
        var g = (byte)(value >> 16);
        var b = (byte)(value >> 8);
        var a = (byte)value;
        return string.Format(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} {3:0.###}",
            r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
