using HizenLabs.Shared.UI;
using System.Collections.Generic;

namespace HizenLabs.Shared.UI.Layouts;

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

    public static StandardLayout Create(Menu menu, MenuSize size, Menu.Layer layer = Menu.Layer.Overlay, bool closeButton = true)
    {
        var shell = GetShell(size, layer, menu.Id, closeButton);
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

    private static readonly Dictionary<(MenuSize, Menu.Layer, string, bool), Shell> _shells = new();

    private static Shell GetShell(MenuSize size, Menu.Layer layer, string menuId, bool closeButton)
    {
        var key = (size, layer, menuId, closeButton);
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

        shell.Payload = MenuShell.Build(menuId, menu =>
        {
            // Root: the destroy anchor, centered box of the requested size; the background
            // panel carries the cursor.
            var root = menu.CreateParent(layer, MenuPosition.Center, menuId);
            var window = root.AddPanel(MenuPosition.Center,
                new MenuOffset(-width / 2f, -height / 2f, width / 2f, height / 2f),
                MenuTheme.Background, needsCursor: true);

            var header = window.AddPanel(new MenuPosition(0f, 0.92f, 1f, 1f), new MenuOffset(8f, 4f, -8f, -8f), MenuTheme.Section, shell.Header);
            if (closeButton)
                header.AddCloseButton(closeTarget: menuId, barHeight: height * 0.08f);

            // Content is a pure container: its children paint it.
            window.AddContainer(new MenuPosition(0f, 0.08f, 1f, 0.92f), new MenuOffset(8f, 4f, -8f, -4f), shell.Content);

            window.AddPanel(new MenuPosition(0f, 0f, 1f, 0.08f), new MenuOffset(8f, 8f, -8f, -4f), MenuTheme.Section, shell.Footer);
        });

        _shells[key] = shell;
        return shell;
    }
}
