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
///
/// Refresh granularity, cheapest wins: a named element takes an Update* patch; the current
/// page swaps via <see cref="CreatePage"/> without touching the chrome; the full shell only
/// goes out when the menu is not already open. Track open menus server-side with MenuViewers
/// and pass closeCommand so close clicks reach the tracking.
/// </summary>
public readonly struct AppLayout
{
    // Header-bar geometry: controls are BarHeight minus 2x ControlPadding tall, vertically
    // centered; the close button is a square of that size inset CloseInset from the right,
    // and header action buttons walk further left from it with SlotGap between.
    private const float BarHeight = 54.44f;
    private const float ControlPadding = 10f;
    private const float CloseInset = 10f;
    private const float SlotGap = 8f;

    /// <summary>Slot-0 start when the close button is present - the page-level button helper
    /// assumes it is (the layout-level one reads the shell's actual value).</summary>
    private const float DefaultActionsRight = CloseInset + (BarHeight - ControlPadding * 2f) + SlotGap;

    // The header's title slot, shared by the shell build and the page-level SetTitle.
    private const float TitleLeft = 20f;
    private const float TitleRight = 210f;

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

    /// <summary>closeCommand runs server-side when the close button is clicked; the close
    /// itself stays client-side. Defaults to the menu's CloseCommand.</summary>
    public static AppLayout Create(Menu menu, Menu.Layer layer = Menu.Layer.Overlay, bool closeButton = true, string closeCommand = null)
    {
        var shell = GetShell(layer, menu.Id, closeButton, closeCommand ?? menu.CloseCommand);
        menu.AttachShell(shell.Payload);
        return new AppLayout(menu, shell);
    }

    /// <summary>
    /// The page-owned regions of an open AppLayout. Content fills the content region; Header
    /// and Footer fill the bars, for content that varies per page (labels, buttons) and shares
    /// the bar with the menu-level title and close button. All three are stable-named roots the
    /// page send re-adds, so switching pages replaces everything the previous page put in them.
    /// </summary>
    public readonly struct Page
    {
        public readonly MenuScope Header;
        public readonly MenuScope Content;
        public readonly MenuScope Footer;

        private readonly Menu _menu;
        private readonly string _appMenuId;

        internal Page(Menu menu, string appMenuId, MenuScope header, MenuScope content, MenuScope footer)
        {
            _menu = menu;
            _appMenuId = appMenuId;
            Header = header;
            Content = content;
            Footer = footer;
        }

        /// <summary>
        /// The header title owned by THIS page, drawn in the same slot as the layout-level
        /// <see cref="AppLayout.SetTitle"/> (use one or the other per menu - they overlap).
        /// Left-side header buttons added BEFORE the title shift it right past them.
        /// </summary>
        public Page SetTitle(string title)
        {
            var shift = _menu.LeftEdgeUsed;
            Header.AddText(new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(TitleLeft + shift, 0f, TitleRight + shift, 0f),
                title, 22, MenuTheme.TitleText, TextAnchor.MiddleLeft, MenuTheme.TitleFont);
            return this;
        }

        /// <summary>
        /// A header-bar action button owned by THIS page: swapping pages replaces it with the
        /// next page's set, so each page declares its own buttons. Same slot geometry as the
        /// layout-level variant (assuming the close button is present) - the two share the
        /// row, so a menu-level button and a page button in the same slot overlap. Omit slot
        /// to take the next one in add order.
        /// </summary>
        public Page AddHeaderButton(string label, string command, MenuSide side = MenuSide.Right, int? slot = null, float width = 90f, Color? background = null, Color? textColor = null)
        {
            PlaceHeaderButton(_menu, Header.Container, DefaultActionsRight, label, command, side, slot, width, background, textColor);
            return this;
        }

        /// <summary>A page-owned header button that navigates to another page of this menu via
        /// the generated wiring's navigation handler - no hand-written handler needed.</summary>
        public Page AddHeaderButton<TPage>(string label, TPage page, MenuSide side = MenuSide.Right, int? slot = null, float width = 90f, Color? background = null, Color? textColor = null)
            where TPage : struct, System.Enum
        {
            return AddHeaderButton(label, NavCommand(_appMenuId, page), side, slot, width, background, textColor);
        }
    }

    /// <summary>
    /// Binds a page inside an ALREADY-OPEN AppLayout, with no shell attached - sending replaces
    /// just the page and the chrome never flickers. The three page roots destroy their previous
    /// copies as part of their own add, so a page send atomically swaps out everything the last
    /// page put in them. Create the page menu with its own id, nested under the app's:
    /// <code>
    /// using var menu = Menu.Create(this, "myplugin.main.page");
    /// var page = AppLayout.CreatePage(menu, "myplugin.main");
    /// page.Content.AddText(...);
    /// menu.Send(player);
    /// </code>
    /// The page id must NOT be the app menu id itself: auto-generated element names are
    /// prefixed by the menu id, and a page send reusing the app's prefix would collide with
    /// elements from the shell open, like the title.
    /// </summary>
    public static Page CreatePage(Menu menu, string appMenuId)
    {
        var header = menu.Scope(new MenuContainer(appMenuId + ".header"))
            .AddContainer(MenuPosition.Full, MenuOffset.Zero, menu.Id + ".header", replace: true);
        var content = menu.Scope(new MenuContainer(appMenuId + ".content"))
            .AddContainer(MenuPosition.Full, MenuOffset.Zero, menu.Id, replace: true);
        var footer = menu.Scope(new MenuContainer(appMenuId + ".footer"))
            .AddContainer(MenuPosition.Full, MenuOffset.Zero, menu.Id + ".footer", replace: true);
        return new Page(menu, appMenuId, header, content, footer);
    }

    /// <summary>The header's main title (22pt bold, left-aligned). Left-side header buttons
    /// added BEFORE the title shift it right past them.</summary>
    public AppLayout SetTitle(string title)
    {
        var shift = _menu.LeftEdgeUsed;
        _menu.CreateText(_shell.Header, new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(TitleLeft + shift, 0f, TitleRight + shift, 0f),
            title, 22, MenuTheme.TitleText, TextAnchor.MiddleLeft, MenuTheme.TitleFont);
        return this;
    }

    /// <summary>The header's mono status summary ("8 TASKS - 6 OK - 1 FAILING").</summary>
    public AppLayout SetSummary(string summary)
    {
        _menu.CreateText(_shell.Summary, MenuPosition.Full, MenuOffset.Zero, summary, 12, MenuTheme.MutedText, TextAnchor.MiddleLeft, MenuFont.DroidSansMono);
        return this;
    }

    /// <summary>
    /// A header-bar action button that persists across page swaps (a page declares its own via
    /// <see cref="Page.AddHeaderButton(string, string, MenuSide, int?, float, Color?, Color?)"/>).
    /// Right-side slot 0 sits just left of the close button and higher slots walk left;
    /// left-side buttons walk right from the bar's left inset. Omit slot to take the next one
    /// in add order.
    /// </summary>
    public AppLayout AddHeaderButton(string label, string command, MenuSide side = MenuSide.Right, int? slot = null, float width = 90f, Color? background = null, Color? textColor = null)
    {
        PlaceHeaderButton(_menu, _shell.Header, _shell.ActionsRight, label, command, side, slot, width, background, textColor);
        return this;
    }

    /// <summary>A header-bar button that navigates to a page of this menu: the command is the
    /// generated wiring's navigation handler, so no hand-written handler is needed.</summary>
    public AppLayout AddHeaderButton<TPage>(string label, TPage page, MenuSide side = MenuSide.Right, int? slot = null, float width = 90f, Color? background = null, Color? textColor = null)
        where TPage : struct, System.Enum
    {
        return AddHeaderButton(label, NavCommand(_menu.Id, page), side, slot, width, background, textColor);
    }

    /// <summary>The generated navigation command for a page of the menu ("id.nav 2"); MenuWire
    /// emits the matching [MenuCommand("&lt;menuId&gt;.nav")] handler.</summary>
    private static string NavCommand<TPage>(string appMenuId, TPage page) where TPage : struct, System.Enum =>
        appMenuId + ".nav " + System.Convert.ToInt32(page);

    /// <summary>Shared slot placement for layout-level and page-level header buttons. Explicit
    /// slots also advance the auto cursor past themselves, so explicit and add-order buttons
    /// mix without overlapping.</summary>
    private static void PlaceHeaderButton(Menu menu, MenuContainer header, float actionsRight, string label, string command, MenuSide side, int? slot, float width, Color? background, Color? textColor)
    {
        var half = (BarHeight - ControlPadding * 2f) / 2f;
        MenuContainer button;
        if (side == MenuSide.Right)
        {
            var index = slot ?? menu.AutoSlotRight;
            menu.AutoSlotRight = index + 1 > menu.AutoSlotRight ? index + 1 : menu.AutoSlotRight;
            var right = -(actionsRight + index * (width + SlotGap));
            button = menu.CreateButton(header, MenuPosition.MiddleRight,
                new MenuOffset(right - width, -half, right, half), command, background ?? MenuTheme.ButtonBackground);
        }
        else
        {
            var left = CloseInset + menu.LeftEdgeUsed;
            menu.LeftEdgeUsed += width + SlotGap;
            button = menu.CreateButton(header, MenuPosition.MiddleLeft,
                new MenuOffset(left, -half, left + width, half), command, background ?? MenuTheme.ButtonBackground);
        }
        menu.CreateText(button, MenuPosition.Full, MenuOffset.Zero, label, MenuTheme.ButtonFontSize, textColor ?? MenuTheme.ButtonText, font: MenuTheme.TitleFont);
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
        /// <summary>Pixels from the panel's right edge to where header slot 0's right edge
        /// starts (past the close button when the layout has one).</summary>
        public float ActionsRight;
    }

    private static readonly Dictionary<(Menu.Layer, string, bool, string), Shell> _shells = new();

    private static Shell GetShell(Menu.Layer layer, string menuId, bool closeButton, string closeCommand)
    {
        var key = (layer, menuId, closeButton, closeCommand);
        if (_shells.TryGetValue(key, out var shell))
            return shell;

        shell = new Shell
        {
            Header = menuId + ".header",
            Content = menuId + ".content",
            Footer = menuId + ".footer",
            Title = menuId + ".title",
            Summary = menuId + ".summary",
            ActionsRight = CloseInset + (closeButton ? (BarHeight - ControlPadding * 2f) + SlotGap : 0f),
        };

        shell.Payload = MenuShell.Build(menuId, menu =>
        {
            // Root: the destroy anchor spanning the layer, dimmed backdrop with the cursor.
            var root = menu.CreateParent(layer, MenuPosition.Full, menuId);
            var backdrop = root.AddPanel(MenuPosition.Full, MenuOffset.Zero, MenuTheme.Backdrop, needsCursor: true);

            // Main window: centered, fixed 1000 x 600, bordered frame.
            var window = backdrop.AddPanel(MenuPosition.Center, new MenuOffset(-540f, -280f, 540f, 280f), MenuTheme.WindowBackground);

            // Header strip: title + summary slots, bottom divider, close button.
            var header = window.AddContainer(new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -BarHeight, 0f, 0f), shell.Header);
            header.AddContainer(new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(TitleLeft, 0f, TitleRight, 0f), shell.Title);
            header.AddContainer(new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(217.78f, 0f, 551.11f, 0f), shell.Summary);
            header.AddPanel(new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, 1f), MenuTheme.Border);

            if (closeButton)
            {
                header.AddCloseButton(closeTarget: menuId, barHeight: BarHeight, inset: CloseInset, padding: ControlPadding, command: closeCommand);
            }

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
