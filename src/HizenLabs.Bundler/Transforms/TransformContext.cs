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

/// <summary>Naming knobs for the platform split. Defaults are Carbon/Oxide for Rust.</summary>
public sealed class TransformOptions
{
    public string CarbonNamespace { get; init; } = "Carbon.Plugins";
    public string OxideNamespace { get; init; } = "Oxide.Plugins";
    public string CarbonBase { get; init; } = "CarbonPlugin";
    public string OxideBase { get; init; } = "RustPlugin";

    /// <summary>The shared marker base the author derives from; swapped for the platform base.</summary>
    public string BaseMarker { get; init; } = "PluginBase";

    /// <summary>Namespace the marker base lives in. Its usings are dropped from the bundle (the
    /// marker is swapped for the platform base, so the import is dead).</summary>
    public string MarkerNamespace { get; init; } = "HizenLabs.Shared";
}
