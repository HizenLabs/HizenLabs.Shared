using Facepunch;
using Oxide.Game.Rust.Cui;
using System;

namespace HizenLabs.Shared.UI;

public partial class Menu : IDisposable, Pool.IPooled
{
    private bool _disposed;

    public static Menu Create(PluginBase plugin, string menuId)
    {
        var menu = Pool.Get<Menu>();
        menu.Init(plugin, menuId);
        return menu;
    }

    private partial void Init(PluginBase plugin, string menuId);

    public static void Close(BasePlayer player, string menuId)
    {
        CuiHelper.DestroyUi(player, menuId);
    }

    public void EnterPool()
    {
        EnterPoolCore();
    }

    private partial void EnterPoolCore();

    public void LeavePool()
    {
        _disposed = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeCore();

        var menu = this;
        Pool.Free(ref menu);
    }

    private partial void DisposeCore();
}
