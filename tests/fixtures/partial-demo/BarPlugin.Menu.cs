namespace HizenLabs.Demo;

public partial class BarPlugin
{
    // Hook-only: nothing in the plugin references this method, so it must ship WITHOUT being
    // reachable - plugin parts are never tree-shaken.
    private void ShowMenuHook(BasePlayer player)
    {
        using var menu = Menu.Create(this, MenuId.Main);
    }

    private static class MenuId
    {
        public const string Main = "bar.main";
    }
}
