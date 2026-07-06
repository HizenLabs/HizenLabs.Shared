using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using HizenLabs.Bundler.Transforms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace HizenLabs.Bundler;

public sealed record BundleRequest(
    string PluginPath,
    IReadOnlyList<string> SharedPaths,
    TransformOptions? Transform = null,
    IReadOnlyList<string>? CarbonRefDirs = null,
    IReadOnlyList<string>? OxideRefDirs = null);

/// <summary>Result of compiling the bundled source under one platform's symbol set + references.</summary>
public sealed record PlatformCheck(string Platform, IReadOnlyList<Diagnostic> Errors)
{
    public bool Compiles => Errors.Count == 0;
}

public sealed record BundleResult(
    string Source,
    IReadOnlyList<string> InlinedTypes,
    IReadOnlyList<PlatformCheck> Checks)
{
    /// <summary>True if every check that ran passed. Vacuously true when no refs were supplied.</summary>
    public bool Compiles => Checks.All(c => c.Compiles);

    /// <summary>Whether any compile-check actually ran (refs were supplied for at least one platform).</summary>
    public bool Checked => Checks.Count > 0;
}

/// <summary>
/// Merges a plugin source file plus the shared library code it transitively uses into a single
/// self-contained .cs. Two phases (see docs/transforms.md):
///   1. Inline  - reachable shared types are pulled in as <c>private</c> nested members of the
///                plugin class (tree-shaking; only what's used). The marker base (PluginBase) is
///                NOT inlined - it is swapped for the platform base in phase 2.
///   2. Transform - the author's neutral namespace and <c>: PluginBase</c> are rewritten into the
///                <c>#if CARBON ... #else ... #endif</c> platform split by the transform pipeline.
/// Optionally compile-checks the emitted file under Carbon and/or Oxide when ref dirs are given.
/// </summary>
public static class Bundler
{
    public static BundleResult Bundle(BundleRequest req)
    {
        var opts = req.Transform ?? new TransformOptions();

        var pluginTree = Parse(req.PluginPath);
        var sharedTrees = req.SharedPaths.Select(Parse).ToList();
        var allTrees = new List<SyntaxTree> { pluginTree };
        allTrees.AddRange(sharedTrees);

        var analysisRefs = TpaRefs();
        var compilation = CSharpCompilation.Create(
            "bundle",
            allTrees,
            analysisRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Map every type declared in the shared trees to its declaration - except the marker base
        // (PluginBase): it resolves to the platform base and is swapped, never inlined.
        var sharedTypes = new Dictionary<INamedTypeSymbol, TypeDeclarationSyntax>(SymbolEqualityComparer.Default);
        var sharedNamespaces = new HashSet<string> { opts.MarkerNamespace };
        foreach (var tree in sharedTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var ns in tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                sharedNamespaces.Add(ns.Name.ToString());
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (decl.Identifier.Text == opts.BaseMarker)
                    continue;
                if (model.GetDeclaredSymbol(decl) is INamedTypeSymbol sym)
                    sharedTypes[sym.OriginalDefinition] = decl;
            }
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

        // Drop usings that pointed at shared namespaces (those types are now nested). The author's
        // own (neutral) namespace is kept - the transform pipeline turns it into the #if split.
        root = RemoveUsings(root, sharedNamespaces);

        // Merge the using directives from the shared files whose types were inlined - a nested
        // type's body still needs them (e.g. `using System;` for AppDomain) and the plugin file
        // only carries its own. Shared-namespace usings are excluded like above.
        root = MergeSharedUsings(root, reachable.Select(t => sharedTypes[t].SyntaxTree), sharedNamespaces);

        var inlinedSource = root.NormalizeWhitespace().ToFullString();
        var source = TransformPipeline.Run(inlinedSource, options: opts);

        // Compile-check the emitted single file under each platform we have references for.
        var checks = new List<PlatformCheck>();
        if (req.CarbonRefDirs is { Count: > 0 } carbon)
            checks.Add(Check("Carbon", source, carbon, defineCarbon: true));
        if (req.OxideRefDirs is { Count: > 0 } oxide)
            checks.Add(Check("Oxide", source, oxide, defineCarbon: false));

        var inlined = reachable.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return new BundleResult(source, inlined, checks);
    }

    private static PlatformCheck Check(string platform, string source, IReadOnlyList<string> refDirs, bool defineCarbon)
    {
        var parse = new CSharpParseOptions(
            LanguageVersion.Latest,
            preprocessorSymbols: defineCarbon ? new[] { "CARBON" } : Array.Empty<string>());
        var tree = CSharpSyntaxTree.ParseText(source, parse);
        var comp = CSharpCompilation.Create(
            $"bundle.check.{platform}",
            new[] { tree },
            LoadRefs(refDirs),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = comp.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        return new PlatformCheck(platform, errors);
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

    private static CompilationUnitSyntax RemoveUsings(CompilationUnitSyntax root, HashSet<string> sharedNamespaces)
    {
        var drop = root.Usings
            .Where(u => u.Name is not null && sharedNamespaces.Any(ns =>
                u.Name.ToString() == ns || u.Name.ToString().StartsWith(ns + ".", StringComparison.Ordinal)))
            .ToArray();
        return root.RemoveNodes(drop, SyntaxRemoveOptions.KeepNoTrivia) ?? root;
    }

    private static CompilationUnitSyntax MergeSharedUsings(
        CompilationUnitSyntax root, IEnumerable<SyntaxTree> inlinedTrees, HashSet<string> sharedNamespaces)
    {
        static string Key(UsingDirectiveSyntax u) =>
            $"{u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)}|{u.Alias?.Name}|{u.Name}";

        bool IsShared(UsingDirectiveSyntax u) =>
            u.Name is not null && sharedNamespaces.Any(ns =>
                u.Name.ToString() == ns || u.Name.ToString().StartsWith(ns + ".", StringComparison.Ordinal));

        var have = new HashSet<string>(root.Usings.Select(Key));
        var add = inlinedTrees
            .Distinct()
            .SelectMany(t => t.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>())
            .Where(u => u.GlobalKeyword.IsKind(SyntaxKind.None) && !IsShared(u))
            .Where(u => have.Add(Key(u)))
            .Select(u => u.WithoutTrivia())
            .ToArray();
        return add.Length > 0 ? root.AddUsings(add) : root;
    }

    /// <summary>The net10 trusted-platform assemblies; used only for the (syntactic) reachability pass.</summary>
    private static IReadOnlyList<MetadataReference> TpaRefs()
    {
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "";
        return tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }

    // Build the reference set for a compile-check from the supplied dirs (a server's exported
    // managed set: game + framework + Carbon/Oxide). The test-env exports a complete, internally
    // consistent managed folder per server - including the game's own mscorlib and a netstandard
    // facade - so it is used as-is. Only when the supplied dirs bring no corlib (e.g. a partial,
    // BCL-less ref dir) do we fall back to the net48 reference pack to fill the BCL.
    private static List<MetadataReference> LoadRefs(IReadOnlyList<string> dirs)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hasCorlib = false;

        void AddDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (var dll in Directory.EnumerateFiles(dir, "*.dll"))
            {
                var name = Path.GetFileName(dll);
                if (byName.ContainsKey(name)) continue;
                if (!IsManagedAssembly(dll)) continue; // skip native PE (e.g. EnterpriseServices thunks)
                byName[name] = dll;
                if (name.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase)) hasCorlib = true;
            }
        }

        foreach (var d in dirs) AddDir(d);
        if (!hasCorlib) AddDir(Net48RefDir());

        return byName.Values
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }

    // True if the file is a managed *assembly* (has CLI metadata with an assembly manifest). The
    // net48 ref pack ships native PE stubs (EnterpriseServices Thunk: no metadata) and module-only
    // PEs (EnterpriseServices Wrapper: metadata but no assembly row) that Roslyn rejects as refs.
    private static bool IsManagedAssembly(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return false;
            return pe.GetMetadataReader().IsAssembly;
        }
        catch
        {
            return false;
        }
    }

    // Locate the Microsoft.NETFramework.ReferenceAssemblies net48 BCL in the NuGet cache. Returns
    // "" if not present (the compile-check is then skipped by LoadRefs producing no BCL).
    private static string Net48RefDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(home, ".nuget", "packages", "microsoft.netframework.referenceassemblies.net48");
        if (!Directory.Exists(root)) return "";
        foreach (var version in Directory.EnumerateDirectories(root))
        {
            var dir = Path.Combine(version, "build", ".NETFramework", "v4.8");
            if (Directory.Exists(dir)) return dir;
        }
        return "";
    }
}
