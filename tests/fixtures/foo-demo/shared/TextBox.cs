namespace HizenLabs.Demo.Shared;

// Reached only via FooService -> proves the bundler follows the reference chain.
internal static class TextBox
{
    public static string Frame(string text) => $"+-- {text} --+";
}
