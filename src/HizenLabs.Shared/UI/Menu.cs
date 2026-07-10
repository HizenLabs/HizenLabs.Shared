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

    public partial void Send(BasePlayer player);

    private partial void Init(PluginBase plugin, string menuId);

    private partial void EnterPoolCore();

    private partial void DisposeCore();

    #endregion

    /// <summary>
    /// Handle to a menu element. The id IS the element's client-side name - the one address that
    /// works for creating children, updating across sends, and (later) the Oxide implementation.
    /// Declare stable ids as constants (see MenuId conventions) and pass them directly; the
    /// implicit conversion lifts them into handles with no allocation.
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
}