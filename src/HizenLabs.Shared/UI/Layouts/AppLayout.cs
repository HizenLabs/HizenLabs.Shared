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

        shell = new Shell
        {
            Header = menuId + ".header",
            Content = menuId + ".content",
            Footer = menuId + ".footer",
            Title = menuId + ".title",
            Summary = menuId + ".summary",
        };

        shell.Payload = MenuShell.Build(menuId, menu =>
        {
            // Root: the destroy anchor spanning the layer, dimmed backdrop with the cursor.
            var root = menu.CreateParent(layer, MenuPosition.Full, menuId);
            var backdrop = root.AddPanel(MenuPosition.Full, MenuOffset.Zero, MenuTheme.Backdrop, needsCursor: true);

            // Main window: centered, fixed 1066.67 x 622.22, bordered frame.
            var window = backdrop.AddPanel(MenuPosition.Center, new MenuOffset(-533.33f, -311.11f, 533.33f, 311.11f), MenuTheme.WindowBackground);

            // Header strip: title + summary slots, bottom divider, close button.
            var header = window.AddContainer(new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -54.44f, 0f, 0f), shell.Header);
            header.AddContainer(new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(20f, 0f, 210f, 0f), shell.Title);
            header.AddContainer(new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(217.78f, 0f, 551.11f, 0f), shell.Summary);
            header.AddPanel(new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, 1f), MenuTheme.Border);
            if (closeButton)
                header.AddCloseButton(closeTarget: menuId, barHeight: 54.44f);

            // Content between the bars (pure container - children paint it).
            window.AddContainer(MenuPosition.Full, new MenuOffset(0f, 45.56f, 0f, -54.44f), shell.Content);

            // Footer strip: top divider + watermark; page/status labels are per-open.
            var footer = window.AddContainer(new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, 45.56f), shell.Footer);
            footer.AddPanel(new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -1f, 0f, 0f), MenuTheme.Border);
            footer.AddText(new MenuPosition(0.5f, 0f, 0.5f, 1f), new MenuOffset(-66.67f, 0f, 66.67f, 0f),
                "hizen.dev", 13, MenuTheme.Watermark, TextAnchor.MiddleCenter, MenuFont.Poxel);

            window.AddBorders(MenuTheme.Border);
        });

        _shells[key] = shell;
        return shell;
    }
}
