using System.Collections.Generic;

namespace HizenLabs.Shared.UI;

public enum MenuSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// The general-purpose full menu: centered frame with header, content, and footer sections.
/// <code>
/// var layout = StandardLayout.Create(menu, MenuSize.Medium);
/// layout.Header.AddTitle("Task Scheduler");
/// layout.Content.AddPanel(...);
/// </code>
/// The shell compiles once per (size, layer, menu id) and is cached forever; each layout type
/// owns its shell the same way - copy this file's shape to add a new one.
/// </summary>
public readonly struct StandardLayout
{
    public readonly MenuScope Header;
    public readonly MenuScope Content;
    public readonly MenuScope Footer;

    private StandardLayout(MenuScope header, MenuScope content, MenuScope footer)
    {
        Header = header;
        Content = content;
        Footer = footer;
    }

    public static StandardLayout Create(Menu menu, MenuSize size, Menu.Layer layer = Menu.Layer.Overlay)
    {
        var shell = GetShell(size, layer, menu.Id);
        menu.AttachShell(shell.Payload);
        return new StandardLayout(
            menu.Scope(shell.Header),
            menu.Scope(shell.Content),
            menu.Scope(shell.Footer));
    }

    // ---- shell plumbing (one compile per key, immortal cache) ----

    private sealed class Shell
    {
        public byte[] Payload;
        public string Header;
        public string Content;
        public string Footer;
    }

    private static readonly Dictionary<(MenuSize, Menu.Layer, string), Shell> _shells = new();

    private static Shell GetShell(MenuSize size, Menu.Layer layer, string menuId)
    {
        var key = (size, layer, menuId);
        if (_shells.TryGetValue(key, out var shell))
            return shell;

        var (width, height) = size switch
        {
            MenuSize.Small => (600f, 400f),
            MenuSize.Large => (1200f, 760f),
            _ => (900f, 600f),
        };

        shell = new Shell
        {
            Header = menuId + ".header",
            Content = menuId + ".content",
            Footer = menuId + ".footer",
        };

        var background = menuId + ".bg";
        var sb = MenuShell.Begin();
        var count = 0;

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

        MenuJson.BeginElement(sb, ref count, shell.Header, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.92f, 1f, 1f), new MenuOffset(8f, 4f, -8f, -8f));
        MenuJson.Image(sb, MenuTheme.Section);
        MenuJson.EndElement(sb);

        // Content is a pure container (no image): its children paint it.
        MenuJson.BeginElement(sb, ref count, shell.Content, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.08f, 1f, 0.92f), new MenuOffset(8f, 4f, -8f, -4f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Footer, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 1f, 0.08f), new MenuOffset(8f, 8f, -8f, -4f));
        MenuJson.Image(sb, MenuTheme.Section);
        MenuJson.EndElement(sb);

        shell.Payload = MenuShell.Finish(ref sb);
        _shells[key] = shell;
        return shell;
    }
}
