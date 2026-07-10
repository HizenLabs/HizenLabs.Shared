using Carbon.Components;
using Oxide.Core;

namespace HizenLabs.Shared.UI;

public partial class Menu
{
#if CARBON

    #region Fields

    private CUI _cui;
    private string _prefix;

    #endregion

    #region Partial

    public partial void Send(BasePlayer player)
    {
        Interface.Oxide.LogInfo("Sending CUI: " + _cui.v2.ToJson());

        _cui.v2.SendUi(player);
    }

    private partial void Init(PluginBase plugin, string menuId)
    {
        _cui = new(plugin.CuiHandler);
        _prefix = menuId;
    }

    private partial void EnterPoolCore()
    {
        _cui = default;
    }

    private partial void DisposeCore()
    {
        _cui.Dispose();
    }

    #endregion

    #region Containers

    /// <summary>
    /// The menu's root attach point on a client layer. Name it the menu id so closing the menu
    /// destroys the whole tree. Element names are the client-side handles that updates (and the
    /// Oxide implementation) address elements by - use stable constants, not per-send strings.
    /// </summary>
    public MenuContainer CreateParent(Layer layer, LuiPosition position, string name)
    {
        var container = _cui.v2.CreateParent((CUI.ClientPanels)layer, position, name);

        return new MenuContainer(container);
    }

    public MenuContainer CreatePanel(
        MenuContainer parent,
        LuiPosition position,
        LuiOffset offset,
        string color,
        string name = "",
        bool needsCursor = false,
        bool needsKeyboard = false)
    {
        var container = _cui.v2.CreatePanel(
            parent.Container,
            position,
            offset,
            MenuColor.Normalize(color),
            name);

        if (needsCursor)
        {
            container.AddCursor();
        }

        if (needsKeyboard)
        {
            container.AddKeyboard();
        }

        return new(container);
    }

    public MenuContainer UpdatePanel(
        string panelId,
        LuiPosition position,
        LuiOffset offset,
        string color)
    {
        var container = _cui.v2.Update(panelId);

        container.SetAnchorAndOffset(position, offset);
        container.SetColor(MenuColor.Normalize(color));

        return new(container);
    }

    public readonly struct MenuContainer
    {
        public readonly string Id => Container.name;

        public readonly LUI.LuiContainer Container { get; }

        public MenuContainer(LUI.LuiContainer container)
        {
            Container = container;
        }
    }

    public enum Layer
    {
        Overlay = CUI.ClientPanels.Overlay,
        Overall = CUI.ClientPanels.Overall
    }

    #endregion

#endif
}
