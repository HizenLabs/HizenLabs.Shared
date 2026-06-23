using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// Replaces the author's (neutral) namespace with the platform split:
/// <code>
/// #if CARBON
/// namespace Carbon.Plugins;
/// #else
/// namespace Oxide.Plugins;
/// #endif
/// </code>
/// Only the <c>namespace X;</c> header is touched; the members below it are left in place.
/// Handles the file-scoped form (what our plugins use).
/// </summary>
public sealed class NamespaceTransform : IPluginTransform
{
    public string Name => "namespace";

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        if (ctx.Namespace is not FileScopedNamespaceDeclarationSyntax ns)
            yield break;

        var header = TextSpan.FromBounds(ns.NamespaceKeyword.SpanStart, ns.SemicolonToken.Span.End);
        var o = ctx.Options;
        yield return new TextChange(
            header,
            $"#if CARBON\nnamespace {o.CarbonNamespace};\n#else\nnamespace {o.OxideNamespace};\n#endif");
    }
}
