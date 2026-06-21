namespace HizenLabs.Shared.Fixture;

internal static class Greeter
{
    // Uses Formatter -> proves transitive reachability.
    public static string Hello(string name) => Formatter.Wrap($"Hello, {name}!");
}
