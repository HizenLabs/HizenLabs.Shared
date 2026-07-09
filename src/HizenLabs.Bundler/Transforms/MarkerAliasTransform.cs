using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// The marker base (<c>PluginBase</c>) is never inlined - instead every reference to it (the
/// plugin's base list, parameters in inlined shared code like <c>Menu.Create(PluginBase, ...)</c>)
/// is satisfied by aliasing the marker to the platform's concrete base at the top of the file:
/// <code>
/// #if CARBON
/// using PluginBase = Carbon.Plugins.CarbonPlugin;
/// #else
/// using PluginBase = Oxide.Plugins.RustPlugin;
/// #endif
/// </code>
/// So <c>public class Foo : PluginBase</c> ships as-is and resolves to CarbonPlugin/RustPlugin per
/// platform, exactly what dev-time code compiled against (the shared marker adds no members of its
/// own). No-op when the bundle never mentions the marker (e.g. an explicit platform base).
/// </summary>
public sealed class MarkerAliasTransform : IPluginTransform
{
    public string Name => "marker-alias";

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        var used = ctx.Root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == ctx.Options.BaseMarker);
        if (!used)
            yield break;

        var o = ctx.Options;
        yield return new TextChange(
            new TextSpan(0, 0),
            $"#if CARBON\nusing {o.BaseMarker} = {o.CarbonNamespace}.{o.CarbonBase};\n#else\nusing {o.BaseMarker} = {o.OxideNamespace}.{o.OxideBase};\n#endif\n");
    }
}
