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
///                plugin class (tree-shaking; only what's used). A partial shared type is emitted
///                as one nested partial per source part. Partial parts of the plugin class itself
///                (its folder is passed as a shared dir) become sibling top-level declarations and
///                always ship. The marker base (PluginBase) is NOT inlined - it is aliased to the
///                platform base in phase 2.
///   2. Transform - the transform pipeline rewrites the author's neutral namespace into the
///                <c>#if CARBON ... #else ... #endif</c> platform split and prepends a
///                platform-split <c>using PluginBase = ...</c> alias for any marker references.
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

        // The plugin class: first public type in the entry file. Resolved up front so partial
        // declarations of it found in the shared dirs (the plugin's own folder is passed as one)
        // can be told apart from genuine shared types.
        var root = (CompilationUnitSyntax)pluginTree.GetRoot();
        var pluginType = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(td => td.Modifiers.Any(SyntaxKind.PublicKeyword));
        var pluginSymbol = compilation.GetSemanticModel(pluginTree)
            .GetDeclaredSymbol(pluginType)?.OriginalDefinition;

        // Map every TOP-LEVEL type declared in the shared trees to its declaration(s) - a partial
        // type keeps ALL of its parts. Nested types ride along with their container and are never
        // registered on their own (that would inline them a second time). Excluded:
        //   - the marker base (PluginBase): swapped for the platform base in phase 2, never inlined;
        //   - parts of the plugin class itself: collected separately and emitted as sibling
        //     partials, since nesting them would declare a different type.
        var sharedTypes = new Dictionary<INamedTypeSymbol, List<TypeDeclarationSyntax>>(SymbolEqualityComparer.Default);
        var pluginParts = new List<TypeDeclarationSyntax>();
        var sharedNamespaces = new HashSet<string> { opts.MarkerNamespace };
        foreach (var tree in sharedTrees)
        {
            // The entry file must never come in via a shared dir (it would duplicate the plugin
            // class). Program.cs filters it too; this guards direct API callers.
            if (!string.IsNullOrEmpty(tree.FilePath) &&
                string.Equals(Path.GetFullPath(tree.FilePath), Path.GetFullPath(pluginTree.FilePath), StringComparison.OrdinalIgnoreCase))
                continue;

            var model = compilation.GetSemanticModel(tree);
            foreach (var ns in tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
                sharedNamespaces.Add(ns.Name.ToString());
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (decl.Parent is TypeDeclarationSyntax)
                    continue;
                if (decl.Identifier.Text == opts.BaseMarker)
                    continue;
                if (model.GetDeclaredSymbol(decl) is not INamedTypeSymbol sym)
                    continue;
                if (pluginSymbol is not null && SymbolEqualityComparer.Default.Equals(sym.OriginalDefinition, pluginSymbol))
                {
                    pluginParts.Add(decl);
                    continue;
                }
                if (!sharedTypes.TryGetValue(sym.OriginalDefinition, out var parts))
                    sharedTypes[sym.OriginalDefinition] = parts = new List<TypeDeclarationSyntax>();
                parts.Add(decl);
            }
        }

        // Reachability: BFS from the plugin tree (entry + its partial parts) through shared type
        // bodies. A reference to a nested type marks its OUTERMOST container reachable - members
        // ride along with the container, matching how registration works above.
        var reachable = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();

        void Seed(SyntaxNode scope, SemanticModel model)
        {
            foreach (var node in scope.DescendantNodesAndSelf())
            {
                var info = model.GetSymbolInfo(node);
                var sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                var type = (sym as INamedTypeSymbol ?? sym?.ContainingType)?.OriginalDefinition;
                while (type?.ContainingType is not null)
                    type = type.ContainingType.OriginalDefinition;
                if (type is not null && sharedTypes.ContainsKey(type) && reachable.Add(type))
                    queue.Enqueue(type);
            }
        }

        Seed(pluginTree.GetRoot(), compilation.GetSemanticModel(pluginTree));
        // Plugin parts are part of the plugin, not reachability-gated: a part holding only hook
        // methods is never referenced by anything, but must still ship. They do seed the BFS.
        foreach (var part in pluginParts)
            Seed(part, compilation.GetSemanticModel(part.SyntaxTree));
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            foreach (var decl in sharedTypes[type])
                Seed(decl, compilation.GetSemanticModel(decl.SyntaxTree));
        }

        // Inline reachable shared types as private nested members. A partial type's parts are
        // merged into ONE nested declaration, each part's content wrapped in a
        // #region <source file> for provenance; the merge moves whole part bodies so #if regions
        // carry over verbatim. When a part's directives make the merge unsafe (unbalanced inside
        // the body, or wrapping the declaration itself), the type falls back to one nested
        // partial per part - still-correct output, just less pretty.
        var nested = reachable
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .SelectMany(t =>
            {
                var parts = sharedTypes[t]
                    .OrderByDescending(d => d.BaseList is not null)
                    .ThenBy(d => d.SyntaxTree.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (parts.Count == 1)
                    return new[] { parts[0] };
                var merged = MergeParts(parts.Select(d => (d, (string?)PartLabel(d))).ToList());
                return merged is not null ? new[] { merged } : parts.ToArray();
            })
            .Select(MakePrivateNested)
            .ToArray();

        // The plugin's own partial parts merge INTO the entry class the same way, each wrapped in
        // its #region <source file>. Fallback (unsafe directives): sibling top-level partial
        // declarations after the entry class - the entry stays first either way so the transform
        // pipeline's "first public type" resolution keeps hitting it.
        var orderedPluginParts = pluginParts
            .OrderBy(d => d.SyntaxTree.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var mergedPlugin = orderedPluginParts.Count == 0
            ? null
            : MergeParts(new[] { (pluginType, (string?)null) }
                .Concat(orderedPluginParts.Select(d => (d, (string?)PartLabel(d)))).ToList());

        var siblingParts = mergedPlugin is not null
            ? Array.Empty<MemberDeclarationSyntax>()
            : orderedPluginParts.Select(d => (MemberDeclarationSyntax)d.WithoutLeadingTrivia()).ToArray();

        var newPluginType = (mergedPlugin ?? pluginType).AddMembers(nested);
        if (siblingParts.Length > 0 && pluginType.Parent is BaseNamespaceDeclarationSyntax pluginNs)
        {
            root = root.ReplaceNode(pluginNs, pluginNs.ReplaceNode(pluginType, newPluginType).AddMembers(siblingParts));
        }
        else
        {
            root = root.ReplaceNode(pluginType, newPluginType);
            if (siblingParts.Length > 0)
                root = root.AddMembers(siblingParts);
        }

        // Drop usings that pointed at shared namespaces (those types are now nested). The author's
        // own (neutral) namespace is kept - the transform pipeline turns it into the #if split.
        root = RemoveUsings(root, sharedNamespaces);

        // Merge the using directives from the shared files whose types were inlined (and from the
        // plugin's own partial part files) - a merged type's body still needs them (e.g. `using
        // System;` for AppDomain) and the entry file only carries its own. Shared-namespace usings
        // are excluded like above.
        var mergedTrees = reachable.SelectMany(t => sharedTypes[t]).Concat(pluginParts).Select(d => d.SyntaxTree);
        root = MergeSharedUsings(root, mergedTrees, sharedNamespaces);

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

    /// <summary>Provenance label for a merged part: the last two path segments of its source file
    /// (e.g. "UI/Menu.Carbon.cs"), shown as a #region around the part's content.</summary>
    private static string PartLabel(TypeDeclarationSyntax decl)
    {
        var path = decl.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(path))
            return decl.Identifier.Text;
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', segments.TakeLast(Math.Min(2, segments.Length)));
    }

    /// <summary>
    /// Merges the partial declarations of one type into a single declaration: the first part is
    /// the primary (header, modifiers, attributes); every part contributes its body content -
    /// members plus the trivia between the braces, so #if/#region blocks (including disabled
    /// text) move verbatim - wrapped in a <c>#region label</c> when a label is given. Base lists
    /// are unioned. Returns null when any part's directives make the move unsafe: a directive in
    /// the part's header or after its close brace (e.g. an #if wrapping the whole declaration), a
    /// non-primary part carrying attributes, or a body whose directives aren't balanced.
    /// </summary>
    private static TypeDeclarationSyntax? MergeParts(IReadOnlyList<(TypeDeclarationSyntax Decl, string? Label)> parts)
    {
        if (parts.Any(p => !CanMergePart(p.Decl)))
            return null;
        if (parts.Skip(1).Any(p => p.Decl.AttributeLists.Count > 0))
            return null;

        var members = new List<MemberDeclarationSyntax>();
        var pending = new List<SyntaxTrivia>();
        foreach (var (decl, label) in parts)
        {
            var open = label is null ? Enumerable.Empty<SyntaxTrivia>() : ParseLeadingTrivia($"\n#region {label}\n");
            var close = label is null ? Enumerable.Empty<SyntaxTrivia>() : ParseLeadingTrivia("\n#endregion\n");
            var lead = pending.Concat(open).Concat(decl.OpenBraceToken.TrailingTrivia).ToList();
            var tail = decl.CloseBraceToken.LeadingTrivia.Concat(close).ToList();
            pending = new List<SyntaxTrivia>();

            if (decl.Members.Count > 0)
            {
                var partMembers = decl.Members.ToList();
                partMembers[0] = partMembers[0].WithLeadingTrivia(lead.Concat(partMembers[0].GetLeadingTrivia()));
                var last = partMembers.Count - 1;
                partMembers[last] = partMembers[last].WithTrailingTrivia(partMembers[last].GetTrailingTrivia().Concat(tail));
                members.AddRange(partMembers);
            }
            else
            {
                // A part that is pure trivia under this parse (e.g. its whole body sits inside a
                // #if branch that is disabled text right now) still ships its content.
                pending.AddRange(lead.Concat(tail));
            }
        }

        var baseTypes = parts
            .Where(p => p.Decl.BaseList is not null)
            .SelectMany(p => p.Decl.BaseList!.Types)
            .GroupBy(t => t.ToString())
            .Select(g => g.First().WithoutTrivia())
            .ToList();

        var primary = parts[0].Decl;
        var merged = primary
            .WithMembers(List(members))
            .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(pending));
        return baseTypes.Count > 0
            ? merged.WithBaseList(BaseList(SeparatedList(baseTypes)))
            : merged.WithBaseList(null);
    }

    /// <summary>
    /// True if a part's body content can be moved into a merged declaration without breaking
    /// preprocessor structure: no directives attached to the header tokens or trailing the close
    /// brace (an #if wrapping the whole declaration must stay put), and the directives between
    /// the braces are balanced (#if/#endif, #region/#endregion) so they can travel as a block.
    /// </summary>
    private static bool CanMergePart(TypeDeclarationSyntax part)
    {
        static bool HasDirective(SyntaxTriviaList trivia) => trivia.Any(t => t.IsDirective);

        foreach (var token in part.DescendantTokens())
        {
            if (token == part.OpenBraceToken)
            {
                if (HasDirective(token.LeadingTrivia))
                    return false;
                break;
            }
            if (HasDirective(token.LeadingTrivia) || HasDirective(token.TrailingTrivia))
                return false;
        }
        if (HasDirective(part.CloseBraceToken.TrailingTrivia))
            return false;

        var body = part.OpenBraceToken.TrailingTrivia
            .Concat(part.Members.SelectMany(m => m.DescendantTrivia()))
            .Concat(part.CloseBraceToken.LeadingTrivia);
        int ifDepth = 0, regionDepth = 0;
        foreach (var trivia in body)
        {
            switch (trivia.Kind())
            {
                case SyntaxKind.IfDirectiveTrivia: ifDepth++; break;
                case SyntaxKind.EndIfDirectiveTrivia: if (--ifDepth < 0) return false; break;
                case SyntaxKind.ElseDirectiveTrivia:
                case SyntaxKind.ElifDirectiveTrivia: if (ifDepth == 0) return false; break;
                case SyntaxKind.RegionDirectiveTrivia: regionDepth++; break;
                case SyntaxKind.EndRegionDirectiveTrivia: if (--regionDepth < 0) return false; break;
            }
        }
        return ifDepth == 0 && regionDepth == 0;
    }

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
