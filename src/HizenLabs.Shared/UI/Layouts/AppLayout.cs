using HizenLabs.Shared.UI;
using System.Collections.Generic;
using UnityEngine;

namespace HizenLabs.Shared.UI.Layouts;

/// <summary>
/// The application-window look: a tall centered frame (1152 wide, screen height minus margins)
/// with a bordered header (title + count badge), a bordered content region, and a bordered
/// footer carrying the watermark. Static chrome - frame, borders, watermark, slots - is the
/// precompiled shell; title, badge, and anything time-based are per-open:
/// <code>
/// var app = AppLayout.Create(menu)
///     .SetTitle("TASK SCHEDULER")
///     .SetBadge("12", " TASKS");
/// app.Footer.AddText(MenuPosition.Full, new(-256f, 0f, -16f, 0f), status, MenuTheme.BodyFontSize,
///     MenuTheme.MutedText, TextAnchor.MiddleRight, name: MenuId.ServerTime);
/// </code>
/// A label that refreshes (server time) gets a stable name and a plugin timer that sends
/// menu.UpdateText(MenuId.ServerTime, ...) - refreshing content never re-sends the shell.
/// </summary>
public readonly struct AppLayout
{
    public readonly MenuScope Header;
    public readonly MenuScope Content;
    public readonly MenuScope Footer;

    private readonly Menu _menu;
    private readonly Shell _shell;

    private AppLayout(Menu menu, Shell shell)
    {
        _menu = menu;
        _shell = shell;
        Header = menu.Scope(shell.Header);
        Content = menu.Scope(shell.Content);
        Footer = menu.Scope(shell.Footer);
    }

    public static AppLayout Create(Menu menu, Menu.Layer layer = Menu.Layer.Overlay)
    {
        var shell = GetShell(layer, menu.Id);
        menu.AttachShell(shell.Payload);
        return new AppLayout(menu, shell);
    }

    /// <summary>The header's main title (25pt bold, left-aligned in its slot).</summary>
    public AppLayout SetTitle(string title)
    {
        _menu.CreateText(_shell.Title, MenuPosition.Full, MenuOffset.Zero, title, 25, MenuTheme.TitleText, TextAnchor.MiddleLeft, MenuTheme.TitleFont);
        return this;
    }

    /// <summary>The header's count badge: a right-aligned value and its label ("12" + " TASKS").</summary>
    public AppLayout SetBadge(string value, string label)
    {
        _menu.CreateText(_shell.BadgeValue, MenuPosition.Full, MenuOffset.Zero, value, MenuTheme.BodyFontSize, MenuTheme.MutedText, TextAnchor.MiddleRight, MenuTheme.TitleFont);
        _menu.CreateText(_shell.BadgeLabel, MenuPosition.Full, MenuOffset.Zero, label, MenuTheme.BodyFontSize, MenuTheme.MutedText, TextAnchor.MiddleLeft, MenuTheme.TitleFont);
        return this;
    }

    // ---- shell plumbing (one compile per key, immortal cache) ----

    private sealed class Shell
    {
        public byte[] Payload;
        public string Header;
        public string Content;
        public string Footer;
        public string Title;
        public string BadgeValue;
        public string BadgeLabel;
    }

    private static readonly Dictionary<(Menu.Layer, string), Shell> _shells = new();

    private static Shell GetShell(Menu.Layer layer, string menuId)
    {
        var key = (layer, menuId);
        if (_shells.TryGetValue(key, out var shell))
            return shell;

        var main = menuId + ".main";
        shell = new Shell
        {
            Header = menuId + ".header",
            Content = menuId + ".content",
            Footer = menuId + ".footer",
            Title = menuId + ".title",
            BadgeValue = menuId + ".badge",
            BadgeLabel = menuId + ".badge-label",
        };

        var sb = MenuShell.Begin();
        var count = 0;

        // Root: the destroy anchor spanning the layer.
        MenuJson.BeginElement(sb, ref count, menuId, Menu.LayerName(layer), update: false);
        MenuJson.Rect(sb, MenuPosition.Full, MenuOffset.Zero);
        MenuJson.EndElement(sb);

        // Main window: centered horizontally, screen height minus margins.
        MenuJson.BeginElement(sb, ref count, main, menuId, update: false);
        MenuJson.Rect(sb, new MenuPosition(0.5f, 0f, 0.5f, 1f), new MenuOffset(-576f, 88f, 576f, -64f));
        MenuJson.Image(sb, MenuTheme.WindowBackground);
        MenuJson.Cursor(sb);
        MenuJson.EndElement(sb);

        // Content between header and footer (pure container - children paint it).
        MenuJson.BeginElement(sb, ref count, shell.Content, main, update: false);
        MenuJson.Rect(sb, MenuPosition.Full, new MenuOffset(0f, 48f, 0f, -56f));
        MenuJson.EndElement(sb);

        // Footer strip with the watermark.
        MenuJson.BeginElement(sb, ref count, shell.Footer, main, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, 48f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, menuId + ".watermark", shell.Footer, update: false);
        MenuJson.Rect(sb, new MenuPosition(0.5f, 0.5f, 0.5f, 0.5f), new MenuOffset(-36f, -20f, 36f, 20f));
        MenuJson.Text(sb, "hizen.dev", 12, MenuTheme.Watermark, TextAnchor.MiddleCenter, MenuFont.Poxel);
        MenuJson.EndElement(sb);

        MenuShell.Borders(sb, ref count, shell.Footer, MenuTheme.Border);

        // Header strip with the title and badge slots.
        MenuJson.BeginElement(sb, ref count, shell.Header, main, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -56f, 0f, 0f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Title, shell.Header, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.5f, 0f, 0.5f), new MenuOffset(24f, -24f, 224f, 24f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.BadgeValue, shell.Header, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.5f, 0f, 0.5f), new MenuOffset(224f, -16f, 272f, 16f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.BadgeLabel, shell.Header, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.5f, 0f, 0.5f), new MenuOffset(272f, -16f, 328f, 16f));
        MenuJson.EndElement(sb);

        MenuShell.Borders(sb, ref count, shell.Header, MenuTheme.Border);
        MenuShell.Borders(sb, ref count, main, MenuTheme.Border);

        shell.Payload = MenuShell.Finish(ref sb);
        _shells[key] = shell;
        return shell;
    }
}
