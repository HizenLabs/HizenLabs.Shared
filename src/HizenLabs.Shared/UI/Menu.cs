using Facepunch;
using System;
using System.Text;
using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Pooled CUI menu builder. Elements are written straight into a JSON buffer as they are
/// declared - CUI is a flat list where nesting is just a parent NAME, so the only ordering rule
/// is parents before children, which declaration order gives for free. One platform-neutral
/// implementation: the payload goes out through CuiHelper.AddUi on Carbon and Oxide alike.
///
/// Element names are the client-side handles everything addresses: declare stable ids as
/// constants for anything you will update, and let unnamed elements auto-name. Re-adding an
/// existing name destroys the old element AND its children, so a menu shell is sent once per
/// open and updates go through Update* (never a re-send of the shell).
///
/// The Menu instance is pooled; scopes and handles are structs, so building a menu allocates
/// only auto-generated names and the payload string a send produces.
/// </summary>
public class Menu : IDisposable, Pool.IPooled
{
    #region Fields

    private readonly StringBuilder _sb = new(1024);
    private bool _disposed;
    private string _prefix;
    private int _count;
    private int _autoId;
    private MenuLayouts.CompiledLayout _layout;

    #endregion

    #region Factory

    public static Menu Create(PluginBase plugin, string menuId)
    {
        var menu = Pool.Get<Menu>();
        menu._prefix = menuId;
        return menu;
    }

    public static void Close(BasePlayer player, string menuId)
    {
        Oxide.Game.Rust.Cui.CuiHelper.DestroyUi(player, menuId);
    }

    #endregion

    #region Pooling

    public void EnterPool()
    {
        _sb.Clear();
        _prefix = null;
        _layout = null;
        _count = 0;
        _autoId = 0;
    }

    public void LeavePool()
    {
        _disposed = false;
        _sb.Append('[');
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var menu = this;
        Pool.Free(ref menu);
    }

    #endregion

    #region Send

    public void Send(BasePlayer player)
    {
        // The precompiled shell first (its panels are the parents of everything below), then the
        // dynamic elements. Both are single AddUi RPCs.
        if (_layout is not null)
        {
            Oxide.Game.Rust.Cui.CuiHelper.AddUi(player, _layout.Json);
        }

        if (_count > 0)
        {
            _sb.Append(']');
            Oxide.Game.Rust.Cui.CuiHelper.AddUi(player, _sb.ToString());
            _sb.Length--; // reopen the array so the same menu can send to more players
        }
    }

    #endregion

    #region Layout

    /// <summary>
    /// Attaches a precompiled layout shell (root + header/content/footer) to this menu. The
    /// shell JSON compiles once per (layout, size, layer, menu id) and is cached forever - after
    /// the first open it is sent as-is with zero building work. Returned scopes target the
    /// shell's sub-panels; what goes inside them is this menu's dynamic content.
    /// </summary>
    public MenuLayout CreateLayout(Layouts layout, MenuSize size, Layer layer = Layer.Overlay)
    {
        _layout = MenuLayouts.Get(layout, size, layer, _prefix);
        return new MenuLayout(
            new MenuScope(this, _layout.Root),
            new MenuScope(this, _layout.Header),
            new MenuScope(this, _layout.Content),
            new MenuScope(this, _layout.Footer));
    }

    #endregion

    #region Elements

    /// <summary>
    /// A root attach point on a client layer. Name it the menu id so closing the menu destroys
    /// the whole tree.
    /// </summary>
    public MenuScope CreateParent(Layer layer, MenuPosition position, string name)
    {
        MenuJson.BeginElement(_sb, ref _count, name, LayerName(layer), update: false);
        MenuJson.Rect(_sb, position, MenuOffset.Zero);
        MenuJson.EndElement(_sb);
        return new MenuScope(this, new MenuContainer(name));
    }

    public MenuContainer CreatePanel(
        MenuContainer parent,
        MenuPosition position,
        MenuOffset offset,
        Color color,
        string name = "",
        bool needsCursor = false,
        bool needsKeyboard = false)
    {
        name = EnsureName(name);
        MenuJson.BeginElement(_sb, ref _count, name, parent.Id, update: false);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.Image(_sb, color);
        if (needsCursor)
            MenuJson.Cursor(_sb);
        if (needsKeyboard)
            MenuJson.Keyboard(_sb);
        MenuJson.EndElement(_sb);
        return new MenuContainer(name);
    }

    public MenuContainer CreateText(
        MenuContainer parent,
        MenuPosition position,
        MenuOffset offset,
        string text,
        int fontSize,
        Color color,
        TextAnchor align = TextAnchor.MiddleCenter,
        string name = "")
    {
        name = EnsureName(name);
        MenuJson.BeginElement(_sb, ref _count, name, parent.Id, update: false);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.Text(_sb, text, fontSize, color, align);
        MenuJson.EndElement(_sb);
        return new MenuContainer(name);
    }

    public void UpdatePanel(MenuContainer target, MenuPosition position, MenuOffset offset, Color color)
    {
        MenuJson.BeginElement(_sb, ref _count, target.Id, parent: null, update: true);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.Image(_sb, color);
        MenuJson.EndElement(_sb);
    }

    public void UpdateText(MenuContainer target, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter)
    {
        MenuJson.BeginElement(_sb, ref _count, target.Id, parent: null, update: true);
        MenuJson.Text(_sb, text, fontSize, color, align);
        MenuJson.EndElement(_sb);
    }

    /// <summary>Auto-name for elements the author does not address: "&lt;menuId&gt;.eN". Allocates
    /// one small string per unnamed element per open - name anything you update.</summary>
    private string EnsureName(string name) =>
        string.IsNullOrEmpty(name) ? $"{_prefix}.e{_autoId++}" : name;

    #endregion

    #region Types

    /// <summary>
    /// Handle to a menu element. The id IS the element's client-side name - the one address that
    /// works for creating children and updating across sends, on both platforms. Declare stable
    /// ids as constants; the implicit conversion lifts them into handles with no allocation.
    /// </summary>
    public readonly struct MenuContainer
    {
        public readonly string Id;

        public MenuContainer(string id)
        {
            Id = id;
        }

        public static implicit operator MenuContainer(string id) => new(id);
    }

    /// <summary>
    /// A container-bound builder: Add* methods need no container argument because the scope IS
    /// the container. Structs all the way down - nothing to pool, nothing to leak.
    /// </summary>
    public readonly struct MenuScope
    {
        private readonly Menu _menu;
        public readonly MenuContainer Container;

        internal MenuScope(Menu menu, MenuContainer container)
        {
            _menu = menu;
            Container = container;
        }

        public MenuScope AddPanel(MenuPosition position, MenuOffset offset, Color color, string name = "", bool needsCursor = false, bool needsKeyboard = false) =>
            new(_menu, _menu.CreatePanel(Container, position, offset, color, name, needsCursor, needsKeyboard));

        public MenuScope AddText(MenuPosition position, MenuOffset offset, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, string name = "") =>
            new(_menu, _menu.CreateText(Container, position, offset, text, fontSize, color, align, name));

        /// <summary>Pre-formatted centered text filling the scope - the header one-liner.</summary>
        public MenuScope AddTitle(string text, int fontSize = 18) =>
            AddText(MenuPosition.Full, MenuOffset.Zero, text, fontSize, Color.white);
    }

    /// <summary>The scopes of a precompiled layout shell.</summary>
    public readonly struct MenuLayout
    {
        public readonly MenuScope Root;
        public readonly MenuScope Header;
        public readonly MenuScope Content;
        public readonly MenuScope Footer;

        internal MenuLayout(MenuScope root, MenuScope header, MenuScope content, MenuScope footer)
        {
            Root = root;
            Header = header;
            Content = content;
            Footer = footer;
        }
    }

    public enum Layer
    {
        Overlay,
        Overall,
        Hud,
        Under,
    }

    internal static string LayerName(Layer layer) => layer switch
    {
        Layer.Overall => "Overall",
        Layer.Hud => "Hud",
        Layer.Under => "Under",
        _ => "Overlay",
    };

    #endregion
}
