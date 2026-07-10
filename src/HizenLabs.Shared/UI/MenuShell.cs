using Facepunch;
using System.Text;
using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Helpers for building a layout's shell payload: the static part of a menu (frame, sections,
/// slots) written once with the same element writers dynamic content uses, then encoded to an
/// immortal byte[] that layout types cache per menu id and hand to <see cref="Menu.AttachShell"/>.
/// Compiling happens once per cache key - the allocations here are one-time by design.
/// </summary>
public static class MenuShell
{
    /// <summary>An open element array to write shell elements into (pass to MenuJson).</summary>
    public static StringBuilder Begin()
    {
        var sb = Pool.Get<StringBuilder>();
        sb.Append('[');
        return sb;
    }

    /// <summary>Four 1px-style border panels around a parent, named "&lt;parent&gt;.border-*".</summary>
    public static void Borders(StringBuilder sb, ref int count, string parent, Color color, float thickness = 1f)
    {
        MenuJson.BeginElement(sb, ref count, parent + ".border-top", parent, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -thickness, 0f, 0f));
        MenuJson.Image(sb, color);
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, parent + ".border-bottom", parent, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, thickness));
        MenuJson.Image(sb, color);
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, parent + ".border-left", parent, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(0f, thickness, thickness, -thickness));
        MenuJson.Image(sb, color);
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, parent + ".border-right", parent, update: false);
        MenuJson.Rect(sb, new MenuPosition(1f, 0f, 1f, 1f), new MenuOffset(-thickness, thickness, 0f, -thickness));
        MenuJson.Image(sb, color);
        MenuJson.EndElement(sb);
    }

    /// <summary>
    /// The standard close button: a square sized to fit its bar, vertically centered, inset from
    /// the right edge, closing the menu root CLIENT-side (the close attribute needs no command,
    /// so this lives in the shell). Size it by the bar's pixel height minus padding.
    /// </summary>
    public static void CloseButton(StringBuilder sb, ref int count, string parent, string closeTarget, float barHeight, float inset = 20f, float padding = 10f)
    {
        var half = (barHeight - padding * 2f) / 2f;
        var name = parent + ".close";

        MenuJson.BeginElement(sb, ref count, name, parent, update: false);
        MenuJson.Rect(sb, new MenuPosition(1f, 0.5f, 1f, 0.5f), new MenuOffset(-inset - half * 2f, -half, -inset, half));
        MenuJson.Button(sb, command: null, MenuTheme.ButtonBackground, close: closeTarget);
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, name + ".x", name, update: false);
        MenuJson.Rect(sb, MenuPosition.Full, MenuOffset.Zero);
        MenuJson.Text(sb, "\u2715", 14, MenuTheme.ButtonText, TextAnchor.MiddleCenter, MenuFont.RobotoCondensedBold);
        MenuJson.EndElement(sb);
    }

    /// <summary>Closes the array and encodes the ready-to-send payload.</summary>
    public static byte[] Finish(ref StringBuilder sb)
    {
        sb.Append(']');
        var chars = new char[sb.Length];
        sb.CopyTo(0, chars, 0, sb.Length);
        var bytes = Encoding.UTF8.GetBytes(chars, 0, chars.Length);
        Pool.FreeUnmanaged(ref sb);
        return bytes;
    }
}
