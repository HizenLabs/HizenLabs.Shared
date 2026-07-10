using System.Collections.Generic;
using System.Text;

namespace HizenLabs.Shared.UI;

public enum Layouts
{
    Default,
}

public enum MenuSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// Precompiled layout shells. A shell (root + background + header/content/footer panels) never
/// changes for a given (layout, size, layer, menu id), so it compiles to its JSON payload once
/// and lives in a static cache - every open after the first sends the cached string with zero
/// building work. Dynamic content goes inside the shell's sub-panels through the menu's writer.
/// </summary>
public static class MenuLayouts
{
    /// <summary>A compiled shell: the ready-to-send UTF-8 payload plus the ids of its scopes.
    /// Bytes are pre-encoded so opening a menu sends immortal cache with zero building work.</summary>
    public sealed class CompiledLayout
    {
        public readonly byte[] Bytes;
        public readonly string Root;
        public readonly string Header;
        public readonly string Content;
        public readonly string Footer;

        internal CompiledLayout(byte[] bytes, string root, string header, string content, string footer)
        {
            Bytes = bytes;
            Root = root;
            Header = header;
            Content = content;
            Footer = footer;
        }
    }

    private static readonly Dictionary<(Layouts, MenuSize, Menu.Layer, string), CompiledLayout> _cache = new();

    public static CompiledLayout Get(Layouts layout, MenuSize size, Menu.Layer layer, string menuId)
    {
        var key = (layout, size, layer, menuId);
        if (_cache.TryGetValue(key, out var compiled))
            return compiled;

        compiled = Compile(size, layer, menuId);
        _cache[key] = compiled;
        return compiled;
    }

    private static CompiledLayout Compile(MenuSize size, Menu.Layer layer, string menuId)
    {
        var (width, height) = size switch
        {
            MenuSize.Small => (600f, 400f),
            MenuSize.Large => (1200f, 760f),
            _ => (900f, 600f),
        };

        var background = menuId + ".bg";
        var header = menuId + ".header";
        var content = menuId + ".content";
        var footer = menuId + ".footer";

        // One-time compile per cache key.
        var sb = Facepunch.Pool.Get<StringBuilder>();
        var count = 0;
        sb.Append('[');

        // Root: the destroy anchor, centered box of the requested size.
        MenuJson.BeginElement(sb, ref count, menuId, Menu.LayerName(layer), update: false);
        MenuJson.Rect(sb, MenuPosition.Center, new MenuOffset(-width / 2f, -height / 2f, width / 2f, height / 2f));
        MenuJson.EndElement(sb);

        // Background panel carries the cursor - a menu you can't click is rarely wanted.
        MenuJson.BeginElement(sb, ref count, background, menuId, update: false);
        MenuJson.Rect(sb, MenuPosition.Full, MenuOffset.Zero);
        MenuJson.Image(sb, MenuTheme.Background);
        MenuJson.Cursor(sb);
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, header, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.92f, 1f, 1f), new MenuOffset(8f, 4f, -8f, -8f));
        MenuJson.Image(sb, MenuTheme.Section);
        MenuJson.EndElement(sb);

        // Content is a pure container (no image): its children paint it.
        MenuJson.BeginElement(sb, ref count, content, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.08f, 1f, 0.92f), new MenuOffset(8f, 4f, -8f, -4f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, footer, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 1f, 0.08f), new MenuOffset(8f, 8f, -8f, -4f));
        MenuJson.Image(sb, MenuTheme.Section);
        MenuJson.EndElement(sb);

        sb.Append(']');
        // One-time per cache key: encode straight to the immortal byte payload.
        var chars = new char[sb.Length];
        sb.CopyTo(0, chars, 0, sb.Length);
        var bytes = Encoding.UTF8.GetBytes(chars, 0, chars.Length);
        Facepunch.Pool.FreeUnmanaged(ref sb);
        return new CompiledLayout(bytes, menuId, header, content, footer);
    }
}
