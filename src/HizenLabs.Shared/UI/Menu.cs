using Facepunch;
using System;

namespace HizenLabs.Shared.UI;

public partial class Menu : IDisposable, Pool.IPooled
{
    #region Fields

    private bool _disposed;

    #endregion

    #region Factory

    public static Menu Create(PluginBase plugin, string menuId)
    {
        var menu = Pool.Get<Menu>();
        menu.Init(plugin, menuId);
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
        EnterPoolCore();
    }

    public void LeavePool()
    {
        _disposed = false;
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeCore();

        var menu = this;
        Pool.Free(ref menu);
    }

    #endregion

    #region Framework Methods

    private partial void Init(PluginBase plugin, string menuId);

    private partial void EnterPoolCore();

    private partial void DisposeCore();

    #endregion
}