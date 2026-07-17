using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// Marker types (<see cref="TypeMarker"/>) are never inlined - instead every reference to one
/// (the plugin's base list, [MenuCommand] usages, parameters in inlined shared code like
/// <c>Menu.Create(PluginBase, ...)</c>) is satisfied by aliasing the marker to the platform's
/// concrete type at the top of the file:
/// <code>
/// #if CARBON
/// using PluginBase = Carbon.Plugins.CarbonPlugin;
/// using MenuCommandAttribute = ProtectedCommandAttribute;
/// #else
/// using PluginBase = Oxide.Plugins.RustPlugin;
/// using MenuCommandAttribute = Oxide.Plugins.ConsoleCommandAttribute;
/// #endif
/// </code>
/// So marker references ship as-is and resolve per platform, exactly what dev-time code
/// compiled against (the shared markers add no members of their own). Only markers the bundle
/// actually mentions are aliased; an attribute marker also counts as used via its short form
/// ([MenuCommand] for MenuCommandAttribute). No-op when no marker is used.
/// </summary>
public sealed class MarkerAliasTransform : IPluginTransform
{
    public string Name => "marker-alias";

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        var identifiers = new HashSet<string>(
            ctx.Root.DescendantNodes().OfType<IdentifierNameSyntax>().Select(id => id.Identifier.Text));

        var used = ctx.Options.Markers.Where(m =>
                identifiers.Contains(m.Name)
                || (m.Name.EndsWith("Attribute", StringComparison.Ordinal)
                    && identifiers.Contains(m.Name[..^"Attribute".Length])))
            .ToList();
        if (used.Count == 0)
            yield break;

        var carbon = string.Concat(used.Select(m => $"using {m.Name} = {m.CarbonTarget};\n"));
        var oxide = string.Concat(used.Select(m => $"using {m.Name} = {m.OxideTarget};\n"));
        yield return new TextChange(
            new TextSpan(0, 0),
            $"#if CARBON\n{carbon}#else\n{oxide}#endif\n");
    }
}
