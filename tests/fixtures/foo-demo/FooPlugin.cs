using HizenLabs.Shared;
using HizenLabs.Demo.Shared;

namespace HizenLabs.Demo;

// Demo plugin authored the HizenLabs way: platform-neutral namespace, derives from the shared
// PluginBase marker, and leans on shared code. A `/foo` chat command replies with text built by
// the shared service. See expected.bundled.cs for what the bundler turns this into.
public class FooPlugin : PluginBase
{
    [ChatCommand("foo")]
    private void FooCommand(BasePlayer player, string command, string[] args)
    {
        var name = args.Length > 0 ? args[0] : "world";
        player.ChatMessage(FooService.Greeting(name));
    }
}
