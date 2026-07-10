using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Menu color plumbing. API surfaces take <see cref="Color"/>; the CUI wire format is the
/// "r g b a" float string the client parses, produced once per distinct color and cached, so
/// steady-state sends allocate nothing for colors.
/// </summary>
public static class MenuColor
{
    private static readonly Dictionary<Color, string> _cuiCache = new();

    /// <summary>The client wire string ("r g b a") for a color, cached per distinct value.</summary>
    public static string ToCui(Color color)
    {
        if (_cuiCache.TryGetValue(color, out var cui))
            return cui;

        cui = string.Format(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} {3:0.###}",
            color.r, color.g, color.b, color.a);
        _cuiCache[color] = cui;
        return cui;
    }

    /// <summary>"RRGGBB"/"RRGGBBAA" (optionally '#'-prefixed) to a color. Throws on bad input -
    /// use for literals/config values where a typo should fail loudly at load.</summary>
    public static Color FromHex(string hex)
    {
        if (!TryParseHex(hex, out var color))
            throw new FormatException($"'{hex}' is not RRGGBB/RRGGBBAA hex");
        return color;
    }

    /// <summary>Non-throwing <see cref="FromHex"/> for user/runtime input.</summary>
    public static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex))
            return false;

        var digits = hex[0] == '#' ? hex.Substring(1) : hex;
        if (digits.Length != 6 && digits.Length != 8)
            return false;
        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        if (digits.Length == 6)
            value = (value << 8) | 0xFF; // no alpha digits: opaque

        color = new Color(
            (byte)(value >> 24) / 255f,
            (byte)(value >> 16) / 255f,
            (byte)(value >> 8) / 255f,
            (byte)value / 255f);
        return true;
    }
}
