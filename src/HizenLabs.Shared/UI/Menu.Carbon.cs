namespace HizenLabs.Shared.UI;

public partial class Menu
{
#if CARBON

    private Carbon.Components.CUI _cui;

    private partial void Init(PluginBase plugin, string menuId)
    {
        _cui = new(plugin.CuiHandler);
    }

    private partial void EnterPoolCore()
    {
        _cui = default;
    }

    private partial void DisposeCore()
    {
        _cui.Dispose();
    }

#endif
}
