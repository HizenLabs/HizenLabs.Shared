using HizenLabs.Shared.UI;
using System.Collections.Generic;
using UnityEngine;

namespace HizenLabs.Shared.UI.Layouts;

/// <summary>
/// The application-window look: a dimmed full-screen backdrop behind a centered 1067x622 window
/// with a bordered frame, a header (title + mono summary, divider, close button), a content
/// region, and a footer (divider + watermark). Static chrome is the precompiled shell; title,
/// summary, and anything time-based are per-open:
/// <code>
/// var app = AppLayout.Create(menu)
///     .SetTitle("TASK SCHEDULER")
///     .SetSummary("8 TASKS - 6 OK - 1 FAILING");
/// app.Footer.AddText(MenuPosition.Full, new(-353f, 0f, -20f, 0f), status, MenuTheme.BodyFontSize,
///     MenuTheme.MutedText, TextAnchor.MiddleRight, MenuFont.DroidSansMono, name: MenuId.ServerTime);
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

    public static AppLayout Create(Menu menu, Menu.Layer layer = Menu.Layer.Overlay, bool closeButton = true)
    {
        var shell = GetShell(layer, menu.Id, closeButton);
        menu.AttachShell(shell.Payload);
        return new AppLayout(menu, shell);
    }

    /// <summary>The header's main title (22pt bold, left-aligned in its slot).</summary>
    public AppLayout SetTitle(string title)
    {
        _menu.CreateText(_shell.Title, MenuPosition.Full, MenuOffset.Zero, title, 22, MenuTheme.TitleText, TextAnchor.MiddleLeft, MenuTheme.TitleFont);
        return this;
    }

    /// <summary>The header's mono status summary ("8 TASKS - 6 OK - 1 FAILING").</summary>
    public AppLayout SetSummary(string summary)
    {
        _menu.CreateText(_shell.Summary, MenuPosition.Full, MenuOffset.Zero, summary, 12, MenuTheme.MutedText, TextAnchor.MiddleLeft, MenuFont.DroidSansMono);
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
        public string Summary;
    }

    private static readonly Dictionary<(Menu.Layer, string, bool), Shell> _shells = new();

    private static Shell GetShell(Menu.Layer layer, string menuId, bool closeButton)
    {
        var key = (layer, menuId, closeButton);
        if (_shells.TryGetValue(key, out var shell))
            return shell;

        var main = menuId + ".main";
        var backdrop = menuId + ".backdrop";
        shell = new Shell
        {
            Header = menuId + ".header",
            Content = menuId + ".content",
            Footer = menuId + ".footer",
            Title = menuId + ".title",
            Summary = menuId + ".summary",
        };

        var sb = MenuShell.Begin();
        var count = 0;

        // Root: the destroy anchor spanning the layer.
        MenuJson.BeginElement(sb, ref count, menuId, Menu.LayerName(layer), update: false);
        MenuJson.Rect(sb, MenuPosition.Full, MenuOffset.Zero);
        MenuJson.EndElement(sb);

        // Dimmed backdrop behind the window; carries the cursor.
        MenuJson.BeginElement(sb, ref count, backdrop, menuId, update: false);
        MenuJson.Rect(sb, MenuPosition.Full, MenuOffset.Zero);
        MenuJson.Image(sb, MenuTheme.Backdrop);
        MenuJson.Cursor(sb);
        MenuJson.EndElement(sb);

        // Main window: centered, fixed 1066.67 x 622.22.
        MenuJson.BeginElement(sb, ref count, main, backdrop, update: false);
        MenuJson.Rect(sb, MenuPosition.Center, new MenuOffset(-533.33f, -311.11f, 533.33f, 311.11f));
        MenuJson.Image(sb, MenuTheme.WindowBackground);
        MenuJson.EndElement(sb);

        // Header strip: title + summary slots, bottom divider, close button.
        MenuJson.BeginElement(sb, ref count, shell.Header, main, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -54.44f, 0f, 0f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Title, shell.Header, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(20f, 0f, 210f, 0f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Summary, shell.Header, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(217.78f, 0f, 551.11f, 0f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Header + ".divider", shell.Header, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, 1f));
        MenuJson.Image(sb, MenuTheme.Border);
        MenuJson.EndElement(sb);

        if (closeButton)
            MenuShell.CloseButton(sb, ref count, shell.Header, closeTarget: menuId, barHeight: 54.44f);

        // Content between header and footer (pure container - children paint it).
        MenuJson.BeginElement(sb, ref count, shell.Content, main, update: false);
        MenuJson.Rect(sb, MenuPosition.Full, new MenuOffset(0f, 45.56f, 0f, -54.44f));
        MenuJson.EndElement(sb);

        // Footer strip: top divider + watermark; page/status labels are per-open via the scope.
        MenuJson.BeginElement(sb, ref count, shell.Footer, main, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, 45.56f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Footer + ".divider", shell.Footer, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -1f, 0f, 0f));
        MenuJson.Image(sb, MenuTheme.Border);
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, menuId + ".watermark", shell.Footer, update: false);
        MenuJson.Rect(sb, new MenuPosition(0.5f, 0f, 0.5f, 1f), new MenuOffset(-66.67f, 0f, 66.67f, 0f));
        MenuJson.Text(sb, "hizen.dev", 13, MenuTheme.Watermark, TextAnchor.MiddleCenter, MenuFont.Poxel);
        MenuJson.EndElement(sb);

        MenuShell.Borders(sb, ref count, main, MenuTheme.Border);

        shell.Payload = MenuShell.Finish(ref sb);
        _shells[key] = shell;
        return shell;
    }
}
