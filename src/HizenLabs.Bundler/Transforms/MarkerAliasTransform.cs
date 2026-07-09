using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// Inlined shared code may reference the marker base (<c>PluginBase</c>) beyond the plugin's base
/// list - e.g. <c>Menu.Create(PluginBase plugin, ...)</c>. The base list is swapped by
/// <see cref="BaseClassTransform"/>, but those other references would dangle (the marker class is
/// never inlined). When any exist, alias the marker to the platform base at the top of the file:
/// <code>
/// #if CARBON
/// using PluginBase = Carbon.Plugins.CarbonPlugin;
/// #else
/// using PluginBase = Oxide.Plugins.RustPlugin;
/// #endif
/// </code>
/// The shared marker adds no members of its own, so the alias is exactly what dev-time code
/// compiled against. No-op when the bundle never mentions the marker outside the base list.
/// </summary>
public sealed class MarkerAliasTransform : IPluginTransform
{
    public string Name => "marker-alias";

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        var baseListMarker = ctx.PluginClass?.BaseList?.Types
            .Select(t => t.Type)
            .OfType<IdentifierNameSyntax>()
            .FirstOrDefault(id => id.Identifier.Text == ctx.Options.BaseMarker);

        var hasOtherRefs = ctx.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == ctx.Options.BaseMarker && id != baseListMarker);
        if (!hasOtherRefs)
            yield break;

        var o = ctx.Options;
        yield return new TextChange(
            new TextSpan(0, 0),
            $"#if CARBON\nusing {o.BaseMarker} = {o.CarbonNamespace}.{o.CarbonBase};\n#else\nusing {o.BaseMarker} = {o.OxideNamespace}.{o.OxideBase};\n#endif\n");
    }
}
