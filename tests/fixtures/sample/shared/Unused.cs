namespace HizenLabs.Shared.Fixture;

// Nothing references this -> the bundler must tree-shake it out.
internal static class Unused
{
    public static int Answer() => 42;
}
