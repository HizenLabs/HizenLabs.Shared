using HizenLabs.Bundler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        // The platform split is present for both the namespace and the marker alias.
        Assert.Contains("#if CARBON", result.Source);
        Assert.Contains("namespace Carbon.Plugins;", result.Source);
        Assert.Contains("namespace Oxide.Plugins;", result.Source);
        Assert.Contains("using PluginBase = Carbon.Plugins.CarbonPlugin;", result.Source);
        Assert.Contains("using PluginBase = Oxide.Plugins.RustPlugin;", result.Source);

        // The base list ships as written (the alias resolves it); the marker class itself and its
        // dev-time import are gone.
        Assert.Contains(": PluginBase", result.Source);
        Assert.DoesNotContain("class PluginBase", result.Source);
        Assert.DoesNotContain("using HizenLabs", result.Source);

        // Usings from inlined shared files are merged in (FooService needs System for AppDomain);
        // without this the nested body fails the platform compile with CS0103.
        Assert.Contains("using System;", result.Source);
    }

    // ---- partial-demo: partial types across shared parts and across plugin part files ----

    private static string PartialFixtureDir => Path.Combine(AppContext.BaseDirectory, "fixtures", "partial-demo");

    private static BundleResult BundlePartialDemo()
    {
        var dir = PartialFixtureDir;
        var plugin = Path.Combine(dir, "BarPlugin.cs");
        // Mirror the deploy target: the shared runtime dir AND the plugin's own folder are both
        // shared dirs (the entry file is excluded, like Program.cs does).
        var shared = Directory.EnumerateFiles(Path.Combine(dir, "shared"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
            .Where(f => !string.Equals(Path.GetFullPath(f), Path.GetFullPath(plugin), StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Bundler.Bundle(new BundleRequest(plugin, shared));
    }

    [Fact]
    public void Partial_shared_type_merges_every_part_into_one_nested_declaration()
    {
        var result = BundlePartialDemo();

        // All three Menu parts survive: the core members, the Carbon impl, the Oxide impl.
        Assert.Contains("Create(", result.Source);
        Assert.Contains("CarbonOnly", result.Source);
        Assert.Contains("#if !CARBON", result.Source);

        // Menu.Create keeps its PluginBase parameter, so the marker alias must be emitted (the
        // marker class itself is never inlined).
        Assert.Contains("using PluginBase = Carbon.Plugins.CarbonPlugin;", result.Source);
        Assert.Contains("using PluginBase = Oxide.Plugins.RustPlugin;", result.Source);

        // The parts merge into ONE nested declaration inside the plugin class, each part's
        // content wrapped in a #region naming its source file.
        var root = CSharpSyntaxTree.ParseText(result.Source).GetRoot();
        var menu = Assert.Single(root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t.Identifier.Text == "Menu"));
        Assert.IsAssignableFrom<TypeDeclarationSyntax>(menu.Parent);
        Assert.Contains("#region shared/Menu.cs", result.Source);
        Assert.Contains("#region shared/Menu.Carbon.cs", result.Source);
        Assert.Contains("#region shared/Menu.Oxide.cs", result.Source);

        // The base list survives the merge (only the core part declares it).
        Assert.Contains(menu.BaseList!.Types, t => t.ToString() == "IDisposable");
    }

    [Fact]
    public void Plugin_partial_parts_merge_into_the_entry_class_and_are_never_tree_shaken()
    {
        var result = BundlePartialDemo();
        var root = CSharpSyntaxTree.ParseText(result.Source).GetRoot();

        // ONE BarPlugin declaration: the part file merged into the entry class, region-labeled
        // with its source file - not nested inside itself, not a sibling.
        var bar = Assert.Single(root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t.Identifier.Text == "BarPlugin"));
        Assert.False(bar.Parent is TypeDeclarationSyntax, "plugin part was nested");
        Assert.Contains("#region partial-demo/BarPlugin.Menu.cs", result.Source);

        // The hook-only part member ships even though nothing references it.
        Assert.Contains("ShowMenuHook", result.Source);

        // MenuId rides along inside its part exactly once (not also inlined standalone).
        var menuIds = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t.Identifier.Text == "MenuId").ToList();
        Assert.Single(menuIds);
    }

    private static string Normalize(string s) =>
        string.Join('\n', s.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).TrimEnd();
}
