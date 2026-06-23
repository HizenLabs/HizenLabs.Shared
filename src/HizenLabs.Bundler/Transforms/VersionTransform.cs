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
public sealed class VersionTransform : IPluginTransform
{
    public string Name => "version";

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        if (ctx.PluginClass is null)
            yield break;

        if (string.IsNullOrEmpty(ctx.Options.Version))
            yield break;

        // locate the [Info] attribute above the plugin class, above any
        var infoAttr = ctx.PluginClass.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() == "Info");

        // Info("<name>", "<author>", "<version>")
        if (infoAttr is not null)
        {
            var nameArg = infoAttr.ArgumentList?.Arguments.ElementAtOrDefault(0);
            var authorArg = infoAttr.ArgumentList?.Arguments.ElementAtOrDefault(1);
            var versionArg = infoAttr.ArgumentList?.Arguments.ElementAtOrDefault(2);

            if (nameArg is not null)
            {
                var nameSpan = nameArg.Span;
                var expandedName = ExpandPluginName(ctx.PluginClass!.Identifier.Text);
                yield return new TextChange(nameSpan, $"\"{expandedName}\"");
            }

            if (authorArg is not null)
            {
                var authorSpan = authorArg.Span;
                yield return new TextChange(authorSpan, $"\"hizen\"");
            }

            if (versionArg is not null)
            {
                var versionSpan = versionArg.Span;
                yield return new TextChange(versionSpan, $"\"{ctx.Options.Version}\"");
            }
        }
    }

    /// <summary>
    /// Expands a plugin class name into a human-readable plugin name by inserting spaces before capital letters.
    /// </summary>
    /// <param name="name">The plugin class name.</param>
    /// <returns>The expanded plugin name.</returns>
    private static string ExpandPluginName(string name)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
