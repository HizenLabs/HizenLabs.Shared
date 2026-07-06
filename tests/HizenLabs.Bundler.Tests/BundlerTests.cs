using HizenLabs.Bundler;
using Xunit;

namespace HizenLabs.Bundler.Tests;

/// <summary>
/// End-to-end bundler test: takes the foo-demo fixture (an authored plugin + its shared code) all
/// the way through inline + transform and compares against the committed expected bundle. This
/// pins the whole pipeline - reachability tree-shaking, the #if CARBON namespace/base split, and
/// using cleanup - in one golden file you can read to see exactly what the server gets.
///
/// It does not pass platform references, so no compile-check runs here (that needs a server's
/// managed set, e.g. the test-env's exported refs); structure is asserted instead.
/// </summary>
public class BundlerTests
{
    private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "fixtures", "foo-demo");

    [Fact]
    public void Foo_demo_bundles_to_expected_output()
    {
        var plugin = Path.Combine(FixtureDir, "FooPlugin.cs");
        var sharedDir = Path.Combine(FixtureDir, "shared");
        var expected = File.ReadAllText(Path.Combine(FixtureDir, "expected.bundled.cs"));

        var shared = Directory.EnumerateFiles(sharedDir, "*.cs", SearchOption.AllDirectories).ToList();
        var result = Bundler.Bundle(new BundleRequest(plugin, shared));

        Assert.Equal(Normalize(expected), Normalize(result.Source));
    }

    [Fact]
    public void Foo_demo_inlines_reachable_shared_code_and_tree_shakes_the_rest()
    {
        var plugin = Path.Combine(FixtureDir, "FooPlugin.cs");
        var sharedDir = Path.Combine(FixtureDir, "shared");
        var shared = Directory.EnumerateFiles(sharedDir, "*.cs", SearchOption.AllDirectories).ToList();

        var result = Bundler.Bundle(new BundleRequest(plugin, shared));

        // Reachable shared code is pulled in (FooService -> TextBox); Unused* is shaken out.
        Assert.Equal(new[] { "FooService", "TextBox" }, result.InlinedTypes);
        Assert.DoesNotContain("UnusedHelper", result.Source);

        // The platform split is present for both the namespace and the base class.
        Assert.Contains("#if CARBON", result.Source);
        Assert.Contains("namespace Carbon.Plugins;", result.Source);
        Assert.Contains("namespace Oxide.Plugins;", result.Source);
        Assert.Contains("CarbonPlugin", result.Source);
        Assert.Contains("RustPlugin", result.Source);

        // The marker base and its import are gone (swapped, not inlined).
        Assert.DoesNotContain(": PluginBase", result.Source);
        Assert.DoesNotContain("using HizenLabs", result.Source);

        // Usings from inlined shared files are merged in (FooService needs System for AppDomain);
        // without this the nested body fails the platform compile with CS0103.
        Assert.Contains("using System;", result.Source);
    }

    private static string Normalize(string s) =>
        string.Join('\n', s.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).TrimEnd();
}
