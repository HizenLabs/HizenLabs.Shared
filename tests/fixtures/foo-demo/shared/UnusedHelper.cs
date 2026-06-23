namespace HizenLabs.Demo.Shared;

// Nothing in the plugin (or its reachable shared code) references this, so the bundler must
// tree-shake it out of the emitted file.
internal static class UnusedHelper
{
    public static int Magic() => 7;
}
