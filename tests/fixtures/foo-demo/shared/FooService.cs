using System;

namespace HizenLabs.Demo.Shared;

// Shared logic the plugin calls into. Uses TextBox, so the bundler must pull TextBox in too
// (transitive reachability). The `using System;` (needed for AppDomain) must be merged into the
// bundle - the plugin file doesn't import System, but the inlined body still needs it.
internal static class FooService
{
    public static string Greeting(string name) => TextBox.Frame($"foo says hi, {name}");

    public static string Domain() => AppDomain.CurrentDomain.FriendlyName;
}
