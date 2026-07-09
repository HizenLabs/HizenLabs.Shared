using HizenLabs.Shared;
using HizenLabs.Demo.Shared;

namespace HizenLabs.Demo;

// Partial-plugin fixture: the plugin spans two files (entry + BarPlugin.Menu.cs, fed in via the
// plugin's own folder as a shared dir, exactly like the deploy target does), and the shared Menu
// spans three partial parts with #if platform regions. Pins the partial handling: shared parts all
// ship as nested partials, plugin parts ship as sibling top-level partials, nested types are not
// inlined twice, and a hook-only plugin part survives tree-shaking.
public partial class BarPlugin : PluginBase
{
    [ChatCommand("bar")]
    private void BarCommand(BasePlayer player, string command, string[] args)
    {
        Menu.Close(player, MenuId.Main);
    }
}
