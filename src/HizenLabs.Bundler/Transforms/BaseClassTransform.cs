using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// Swaps the shared marker base (<c>PluginBase</c>) on the plugin class for the platform base:
/// <code>
/// public class Foo : PluginBase
/// // becomes
/// public class Foo :
/// #if CARBON
///     CarbonPlugin
/// #else
///     RustPlugin
/// #endif
/// </code>
/// No-op if the plugin class doesn't list the marker (e.g. it already names a base explicitly).
/// </summary>
public sealed class BaseClassTransform : IPluginTransform
{
    public string Name => "base-class";

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        var marker = ctx.PluginClass?.BaseList?.Types
            .Select(t => t.Type)
            .OfType<IdentifierNameSyntax>()
            .FirstOrDefault(id => id.Identifier.Text == ctx.Options.BaseMarker);
        if (marker is null)
            yield break;

        var o = ctx.Options;
        yield return new TextChange(
            marker.Span,
            $"\n#if CARBON\n    {o.CarbonBase}\n#else\n    {o.OxideBase}\n#endif");
    }
}
