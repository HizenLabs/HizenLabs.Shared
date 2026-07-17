#if CARBON
using PluginBase = Carbon.Plugins.CarbonPlugin;
using MenuCommandAttribute = ProtectedCommandAttribute;
#else
using PluginBase = Oxide.Plugins.RustPlugin;
using MenuCommandAttribute = Oxide.Plugins.ConsoleCommandAttribute;
#endif
using HizenLabs.Shared;

namespace HizenLabs.Plugins.Sample;

public class Sample : PluginBase
{
    [MenuCommand("sample.click")]
    private void OnClick(ConsoleSystem.Arg arg)
    {
    }
}
