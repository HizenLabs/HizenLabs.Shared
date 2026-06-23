#if CARBON
namespace Carbon.Plugins;
#else
namespace Oxide.Plugins;
#endif
// Demo plugin authored the HizenLabs way: platform-neutral namespace, derives from the shared
// PluginBase marker, and leans on shared code. A `/foo` chat command replies with text built by
// the shared service. See expected.bundled.cs for what the bundler turns this into.
[Info("Foo Plugin", "hizen", "1.2.3")]
public class FooPlugin :
#if CARBON
    CarbonPlugin
#else
    RustPlugin
#endif
{
    [ChatCommand("foo")]
    private void FooCommand(BasePlayer player, string command, string[] args)
    {
        var name = args.Length > 0 ? args[0] : "world";
        player.ChatMessage(FooService.Greeting(name));
    }

    private static class FooService
    {
        public static string Greeting(string name) => TextBox.Frame($"foo says hi, {name}");
    }

    private static class TextBox
    {
        public static string Frame(string text) => $"+-- {text} --+";
    }
}