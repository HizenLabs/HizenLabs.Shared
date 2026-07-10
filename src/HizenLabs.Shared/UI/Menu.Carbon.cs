using Carbon.Components;
using Oxide.Core;
using UnityEngine;

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

        return new MenuContainer(container.name);
    }

    public MenuContainer CreatePanel(
        MenuContainer parent,
        LuiPosition position,
        LuiOffset offset,
        Color color,
        string name = "",
        bool needsCursor = false,
        bool needsKeyboard = false)
    {
        var container = _cui.v2.CreatePanel(
            parent.Id,
            position,
            offset,
            MenuColor.ToCui(color),
            name);

        if (needsCursor)
        {
            container.AddCursor();
        }

        if (needsKeyboard)
        {
            container.AddKeyboard();
        }

        return new MenuContainer(container.name);
    }

    public MenuContainer UpdatePanel(
        MenuContainer target,
        LuiPosition position,
        LuiOffset offset,
        Color color)
    {
        var container = _cui.v2.Update(target.Id);

        container.SetAnchorAndOffset(position, offset);
        container.SetColor(MenuColor.ToCui(color));

        return target;
    }

    public enum Layer
    {
        Overlay = CUI.ClientPanels.Overlay,
        Overall = CUI.ClientPanels.Overall
    }

    #endregion

#endif
}
