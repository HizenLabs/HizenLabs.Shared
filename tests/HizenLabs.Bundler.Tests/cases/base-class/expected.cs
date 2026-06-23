using HizenLabs.Shared;

namespace HizenLabs.Plugins.Sample;

public class Sample :
#if CARBON
    CarbonPlugin
#else
    RustPlugin
#endif
{
}
