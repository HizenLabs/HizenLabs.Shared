#if CARBON
using PluginBase = Carbon.Plugins.CarbonPlugin;
#else
using PluginBase = Oxide.Plugins.RustPlugin;
#endif
using HizenLabs.Shared;

namespace HizenLabs.Plugins.Sample;

public class Sample : PluginBase
{
}
