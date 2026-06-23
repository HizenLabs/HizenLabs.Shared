namespace HizenLabs.Demo.Shared;

// Shared logic the plugin calls into. Uses TextBox, so the bundler must pull TextBox in too
// (transitive reachability).
internal static class FooService
{
    public static string Greeting(string name) => TextBox.Frame($"foo says hi, {name}");
}
