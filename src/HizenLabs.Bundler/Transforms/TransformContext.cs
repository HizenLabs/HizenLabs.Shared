using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// Everything a transform needs about one plugin source file: the source text, the parsed root,
/// and the resolved plugin class + namespace. Purely syntactic - the namespace/base transforms
/// need no semantic model, which keeps them (and their tests) free of game/Carbon references.
/// </summary>
public sealed class TransformContext
{
    public required SourceText Text { get; init; }
    public required CompilationUnitSyntax Root { get; init; }

    /// <summary>The plugin's namespace declaration (file-scoped or block), or null if none.</summary>
    public BaseNamespaceDeclarationSyntax? Namespace { get; init; }

    /// <summary>The plugin class: the first public type declaration in the file, or null.</summary>
    public TypeDeclarationSyntax? PluginClass { get; init; }

    public TransformOptions Options { get; init; } = new();

    public static TransformContext Create(string source, TransformOptions? options = null)
    {
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source).GetRoot();
        return new TransformContext
        {
            Text = SourceText.From(source),
            Root = root,
            Namespace = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault(),
            PluginClass = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(t => t.Modifiers.Any(SyntaxKind.PublicKeyword)),
            Options = options ?? new(),
        };
    }
}

/// <summary>
/// A shared marker type that is never inlined: every reference in the bundle is satisfied by a
/// platform-split using alias to the given targets (see <see cref="MarkerAliasTransform"/>).
/// Targets are written exactly as the alias's right-hand side, so global-namespace types are
/// bare names and namespaced types are fully qualified.
/// </summary>
public sealed record TypeMarker(string Name, string CarbonTarget, string OxideTarget);

/// <summary>Naming knobs for the platform split. Defaults are Carbon/Oxide for Rust.</summary>
public sealed class TransformOptions
{
    public string CarbonNamespace { get; init; } = "Carbon.Plugins";
    public string OxideNamespace { get; init; } = "Oxide.Plugins";

    /// <summary>The marker types the shared library declares for dev-time compiles only; the
    /// bundle aliases each to its platform target instead of inlining it. PluginBase is the
    /// plugin's base class; MenuCommandAttribute is the platform's UI command attribute
    /// (protected under Carbon, plain console under Oxide).</summary>
    public IReadOnlyList<TypeMarker> Markers { get; init; } = new[]
    {
        new TypeMarker("PluginBase", "Carbon.Plugins.CarbonPlugin", "Oxide.Plugins.RustPlugin"),
        new TypeMarker("MenuCommandAttribute", "ProtectedCommandAttribute", "Oxide.Plugins.ConsoleCommandAttribute"),
    };

    /// <summary>Namespace the marker base lives in. Its usings are dropped from the bundle (the
    /// marker is swapped for the platform base, so the import is dead).</summary>
    public string MarkerNamespace { get; init; } = "HizenLabs.Shared";

    /// <summary>
    /// The version string injected into the plugin's <c>[Info(...)]</c>. Production resolves this
    /// from the plugin's changelog (<see cref="Changelog.TopVersion"/>, optionally with a dev
    /// revision); the default here is the fixed placeholder the golden tests pin against.
    /// </summary>
    public string Version { get; init; } = "1.2.3";
}
