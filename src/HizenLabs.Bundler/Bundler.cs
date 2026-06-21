using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace HizenLabs.Bundler;

public sealed record BundleRequest(string PluginPath, IReadOnlyList<string> SharedPaths, string TargetNamespace);

public sealed record BundleResult(string Source, IReadOnlyList<string> InlinedTypes, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Compiles => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// Merges a plugin source file plus the shared library code it transitively uses into a single
/// self-contained .cs. Shared types are inlined as <c>private</c> nested members of the plugin
/// class (so plugins loaded into the same server can't collide), only the reachable ones are
/// pulled in (tree-shaking), and the namespace is rewritten to the target (e.g. Carbon.Plugins).
/// </summary>
public static class Bundler
{
    public static BundleResult Bundle(BundleRequest req)
    {
        var pluginTree = Parse(req.PluginPath);
        var sharedTrees = req.SharedPaths.Select(Parse).ToList();
        var allTrees = new List<SyntaxTree> { pluginTree };
        allTrees.AddRange(sharedTrees);

        var refs = ReferenceAssemblies();
        var compilation = CSharpCompilation.Create(
            "bundle",
            allTrees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Map every type declared in the shared trees to its declaration.
        var sharedTypes = new Dictionary<INamedTypeSymbol, TypeDeclarationSyntax>(SymbolEqualityComparer.Default);
        var sharedNamespaces = new HashSet<string>();
        foreach (var tree in sharedTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var ns in tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                sharedNamespaces.Add(ns.Name.ToString());
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                if (model.GetDeclaredSymbol(decl) is INamedTypeSymbol sym)
                    sharedTypes[sym.OriginalDefinition] = decl;
        }

        // Reachability: BFS from the plugin tree through shared type bodies.
        var reachable = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();

        void Seed(SyntaxNode scope, SemanticModel model)
        {
            foreach (var node in scope.DescendantNodesAndSelf())
            {
                var info = model.GetSymbolInfo(node);
                var sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                var type = (sym as INamedTypeSymbol ?? sym?.ContainingType)?.OriginalDefinition;
                if (type is not null && sharedTypes.ContainsKey(type) && reachable.Add(type))
                    queue.Enqueue(type);
            }
        }

        Seed(pluginTree.GetRoot(), compilation.GetSemanticModel(pluginTree));
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            var decl = sharedTypes[type];
            Seed(decl, compilation.GetSemanticModel(decl.SyntaxTree));
        }

        // Build the bundled compilation unit from the plugin tree.
        var root = (CompilationUnitSyntax)pluginTree.GetRoot();

        var nested = reachable
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => MakePrivateNested(sharedTypes[t]))
            .ToArray();

        var pluginType = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(td => td.Modifiers.Any(SyntaxKind.PublicKeyword));
        root = root.ReplaceNode(pluginType, pluginType.AddMembers(nested));

        root = RewriteNamespace(root, req.TargetNamespace);
        root = RemoveUsings(root, sharedNamespaces);

        var source = root.NormalizeWhitespace().ToFullString();

        // Compile-check the emitted single file standalone.
        var checkTree = CSharpSyntaxTree.ParseText(source);
        var check = CSharpCompilation.Create(
            "bundle.check",
            new[] { checkTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = check.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var inlined = reachable.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return new BundleResult(source, inlined, diagnostics);
    }

    private static SyntaxTree Parse(string path) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);

    private static TypeDeclarationSyntax MakePrivateNested(TypeDeclarationSyntax type)
    {
        var keep = type.Modifiers.Where(m =>
            !m.IsKind(SyntaxKind.PublicKeyword) &&
            !m.IsKind(SyntaxKind.InternalKeyword) &&
            !m.IsKind(SyntaxKind.ProtectedKeyword) &&
            !m.IsKind(SyntaxKind.PrivateKeyword) &&
            !m.IsKind(SyntaxKind.FileKeyword));

        var modifiers = TokenList(Token(SyntaxKind.PrivateKeyword)).AddRange(keep);
        return type.WithModifiers(modifiers).WithoutLeadingTrivia();
    }

    private static CompilationUnitSyntax RewriteNamespace(CompilationUnitSyntax root, string target)
    {
        var name = ParseName(target);
        return root.ReplaceNodes(
            root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>(),
            (orig, _) => orig.WithName(name.WithTriviaFrom(orig.Name)));
    }

    private static CompilationUnitSyntax RemoveUsings(CompilationUnitSyntax root, HashSet<string> sharedNamespaces)
    {
        var drop = root.Usings
            .Where(u => u.Name is not null && sharedNamespaces.Any(ns =>
                u.Name.ToString() == ns || u.Name.ToString().StartsWith(ns + ".", StringComparison.Ordinal)))
            .ToArray();
        return root.RemoveNodes(drop, SyntaxRemoveOptions.KeepNoTrivia) ?? root;
    }

    private static IReadOnlyList<MetadataReference> ReferenceAssemblies()
    {
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "";
        return tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }
}
