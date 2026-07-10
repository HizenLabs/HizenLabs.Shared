using HizenLabs.Shared;
using HizenLabs.Demo.Shared;

namespace HizenLabs.Demo;

// Member-shaking fixture: the plugin uses Helper.Used and Widget.Do; everything else in the
// shared types is dead weight the bundler must drop - EXCEPT what only the other platform's
// disabled #if region references (CarbonSide), which must survive.
public class ShakePlugin : PluginBase
{
    private void Init()
    {
        Helper.Used();
        var widget = new Widget();
        widget.Do();
#if CARBON
        Helper.CarbonSide();
#endif
    }
}
