using HizenLabs.Shared.Fixture;

namespace HizenLabs.Plugins;

// Stand-in for a real plugin class. References Greeter (which transitively uses
// Formatter); never touches Unused.
public class MyPlugin
{
    public string Run(string name) => Greeter.Hello(name);
}
