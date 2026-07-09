using System;

namespace HizenLabs.Demo.Shared;

public partial class Menu : IDisposable
{
    public static Menu Create(PluginBase plugin, string menuId)
    {
        return new Menu();
    }

    public static void Close(BasePlayer player, string menuId)
    {
    }

    private partial void DisposeCore();

    public void Dispose()
    {
        DisposeCore();
    }
}
