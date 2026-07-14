using Facepunch;
using Network;
using Oxide.Core;
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

    // Send-path scratch buffers: ONE static pair shared by every Menu of this plugin (plugin code
    // runs on the server's main thread and Send never re-enters, so there is no concurrent use).
    // Grow-on-demand doubling, never shrunk - the discarded arrays are O(log maxMenuSize) over
    // the plugin's LIFETIME, then the high-water buffers serve every send with zero allocation.
    // The BCL ArrayPool cannot be used in game context on EITHER platform: Oxide ships an
    // internalized System.Buffers, and under Carbon the game's mscorlib ALSO defines ArrayPool<T>,
    // making every reference ambiguous (CS0433) with no way to disambiguate from plugin code.
    private static char[] _chars = new char[512];
    private static byte[] _bytes = new byte[1536];
    private StringBuilder _sb;
    private bool _disposed;
    private string _prefix;
    private int _count;
    private int _autoId;
    private byte[] _shell;

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
        // The buffer goes back to the shared pool - idle pooled menus don't pin one each.
        Pool.FreeUnmanaged(ref _sb);
        _prefix = null;
        _shell = null;
        CloseCommand = null;
        _count = 0;
        _autoId = 0;
    }

    public void LeavePool()
    {
        _disposed = false;
        _sb = Pool.Get<StringBuilder>();
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

    private static readonly uint _addUiRpc = StringPool.Get("AddUI");

    public void Send(BasePlayer player)
    {
        // The precompiled shell first (its panels are the parents of everything below), then the
        // dynamic elements. Both go out as raw AddUI RPC bytes; the shell is immortal cache.
        if (_shell is not null)
        {
            SendPayload(player.net.connection, _shell, _shell.Length);
        }

        if (_count > 0)
        {
            // The builder holds an OPEN array ("[" + elements); the closing "]" goes straight
            // into the scratch chars so the builder stays open - the same menu can keep sending
            // (more players) or keep appending elements.
            var length = _sb.Length + 1;
            EnsureCapacity(ref _chars, length);
            _sb.CopyTo(0, _chars, 0, _sb.Length);
            _chars[_sb.Length] = ']';
            EnsureCapacity(ref _bytes, Encoding.UTF8.GetMaxByteCount(length));
            var size = Encoding.UTF8.GetBytes(_chars, 0, length, _bytes, 0);
            SendPayload(player.net.connection, _bytes, size);
        }
    }

    // Doubling growth for the scratch buffers. Deliberately NOT Array.Resize: that copies the
    // old contents into the new array, and these buffers are fully overwritten before every
    // read, so the copy would be pure waste.
    private static void EnsureCapacity<T>(ref T[] array, int minSize)
    {
        if (array.Length >= minSize)
            return;
        array = new T[Math.Max(minSize, array.Length * 2)];
    }

    // The same wire write LUI does: an RPCMessage on the community entity carrying the payload.
    // The length prefix + raw bytes IS BytesWithSize(span, variableLength: false) - written by
    // hand because the game's span types live in its netstandard facade, which a net48 dev
    // compile cannot import, making every BytesWithSize overload uncallable here. NetWrite is a
    // Stream, so the byte window writes directly. Pure game API, identical on Carbon and Oxide.
    internal static void SendPayload(Connection connection, byte[] bytes, int size)
    {
        // The client hard-rejects AddUI payloads over 10 MiB (and a menu anywhere near that
        // needs pagination, not a bigger buffer).
        if (size > 10 * 1024 * 1024)
        {
            Interface.Oxide.LogWarning($"Menu payload too large to send ({size} bytes); dropped.");
            return;
        }

        var write = Net.sv.StartWrite();
        write.PacketID(Message.Type.RPCMessage);
        write.EntityID(CommunityEntity.ServerInstance.net.ID);
        write.UInt32(_addUiRpc);
        write.UInt32((uint)size);
        write.Write(bytes, 0, size);
        write.Send(new SendInfo(connection));
    }

    #endregion

    #region Shells

    /// <summary>
    /// Registers a precompiled shell payload (see <see cref="MenuShell"/>) to send ahead of this
    /// menu's dynamic elements. Layout types (StandardLayout, ConfirmDialog, ...) compile their
    /// shell once per menu id, cache it forever, and attach it here on every open.
    /// </summary>
    public void AttachShell(byte[] shell)
    {
        _shell = shell;
    }

    /// <summary>The menu id this menu was created with (its root/destroy anchor name).</summary>
    public string Id => _prefix;

    /// <summary>
    /// Console command a layout attaches to its close button, so the server hears about the
    /// client-side close. Set before creating the layout; null leaves the close silent.
    /// </summary>
    public string CloseCommand { get; set; }

    /// <summary>A scope bound to an existing element (a shell's slot, a known id).</summary>
    public MenuScope Scope(MenuContainer container) => new(this, container);

    /// <summary>A button handle bound to an existing slot (see <see cref="MenuButton"/>).</summary>
    public MenuButton Button(MenuContainer slot) => new(this, slot);

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

    /// <summary>A pure positioning container (no visual) - section bounds, slots, spacers.</summary>
    public MenuContainer CreateContainer(MenuContainer parent, MenuPosition position, MenuOffset offset, string name = "")
    {
        name = EnsureName(name);
        MenuJson.BeginElement(_sb, ref _count, name, parent.Id, update: false);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.EndElement(_sb);
        return new MenuContainer(name);
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
        MenuFont font = MenuFont.RobotoCondensedRegular,
        string name = "")
    {
        name = EnsureName(name);
        MenuJson.BeginElement(_sb, ref _count, name, parent.Id, update: false);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.Text(_sb, text, fontSize, color, align, font);
        MenuJson.EndElement(_sb);
        return new MenuContainer(name);
    }

    /// <summary>
    /// A clickable region running a command. Under Carbon the command resolves through the
    /// protected-command table (pair handlers with [MenuCommand]); under Oxide it is the plain
    /// console command name.
    /// </summary>
    public MenuContainer CreateButton(
        MenuContainer parent,
        MenuPosition position,
        MenuOffset offset,
        string command,
        Color color,
        string name = "")
    {
        name = EnsureName(name);
#if CARBON
        command = Carbon.Community.Protect(command);
#endif
        MenuJson.BeginElement(_sb, ref _count, name, parent.Id, update: false);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.Button(_sb, command, color);
        MenuJson.EndElement(_sb);
        return new MenuContainer(name);
    }

    /// <summary>Four border panels around the parent, named "&lt;parent&gt;.border-*".</summary>
    public void CreateBorders(MenuContainer parent, Color color, float thickness = 1f)
    {
        CreatePanel(parent, new MenuPosition(0f, 1f, 1f, 1f), new MenuOffset(0f, -thickness, 0f, 0f), color, parent.Id + ".border-top");
        CreatePanel(parent, new MenuPosition(0f, 0f, 1f, 0f), new MenuOffset(0f, 0f, 0f, thickness), color, parent.Id + ".border-bottom");
        CreatePanel(parent, new MenuPosition(0f, 0f, 0f, 1f), new MenuOffset(0f, thickness, thickness, -thickness), color, parent.Id + ".border-left");
        CreatePanel(parent, new MenuPosition(1f, 0f, 1f, 1f), new MenuOffset(-thickness, thickness, 0f, -thickness), color, parent.Id + ".border-right");
    }

    /// <summary>
    /// The standard close button: a square sized to its bar (height minus 2x padding),
    /// vertically centered, inset from the right, closing the target CLIENT-side (no
    /// round-trip). Pass a command to also notify the server of the close; the client
    /// runs both.
    /// </summary>
    public MenuContainer CreateCloseButton(MenuContainer parent, string closeTarget, float barHeight, float inset = 20f, float padding = 10f, string command = null)
    {
        var half = (barHeight - padding * 2f) / 2f;
        var name = parent.Id + ".close";

#if CARBON
        if (command is not null)
            command = Carbon.Community.Protect(command);
#endif
        MenuJson.BeginElement(_sb, ref _count, name, parent.Id, update: false);
        MenuJson.Rect(_sb, new MenuPosition(1f, 0.5f, 1f, 0.5f), new MenuOffset(-inset - half * 2f, -half, -inset, half));
        MenuJson.Button(_sb, command, MenuTheme.ButtonBackground, close: closeTarget);
        MenuJson.EndElement(_sb);

        CreateText(new MenuContainer(name), MenuPosition.Full, MenuOffset.Zero, "\u2715", 14, MenuTheme.ButtonText, TextAnchor.MiddleCenter, MenuTheme.TitleFont, name + ".x");
        return new MenuContainer(name);
    }

    public void UpdatePanel(MenuContainer target, MenuPosition position, MenuOffset offset, Color color)
    {
        MenuJson.BeginElement(_sb, ref _count, target.Id, parent: null, update: true);
        MenuJson.Rect(_sb, position, offset);
        MenuJson.Image(_sb, color);
        MenuJson.EndElement(_sb);
    }

    public void UpdateText(MenuContainer target, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, MenuFont font = MenuFont.RobotoCondensedRegular)
    {
        MenuJson.BeginElement(_sb, ref _count, target.Id, parent: null, update: true);
        MenuJson.Text(_sb, text, fontSize, color, align, font);
        MenuJson.EndElement(_sb);
    }

    /// <summary>Encodes the buffer as a finished element array - the immortal payload a layout
    /// caches. Shell building only (see MenuShell.Build); one-time exact-size allocation.</summary>
    internal byte[] ExportShell()
    {
        var length = _sb.Length + 1;
        EnsureCapacity(ref _chars, length);
        _sb.CopyTo(0, _chars, 0, _sb.Length);
        _chars[_sb.Length] = ']';
        var bytes = new byte[Encoding.UTF8.GetByteCount(_chars, 0, length)];
        Encoding.UTF8.GetBytes(_chars, 0, length, bytes, 0);
        return bytes;
    }

    /// <summary>Auto-name for elements the author does not address: "&lt;menuId&gt;.eN". Allocates
    /// one small string per unnamed element per open - name anything you update.</summary>
    private string EnsureName(string name) =>
        string.IsNullOrEmpty(name) ? $"{_prefix}.e{_autoId++}" : name;

    #endregion

    #region Types

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
