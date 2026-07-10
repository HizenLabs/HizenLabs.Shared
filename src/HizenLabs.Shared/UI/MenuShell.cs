using Facepunch;
using System.Text;

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
