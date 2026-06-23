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

        var expandedName = ExpandPluginName(ctx.PluginClass.Identifier.Text);

        // locate the [Info] attribute above the plugin class, above any
        var infoAttr = ctx.PluginClass.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() == "Info");

        // No [Info] at all: synthesize one on its own line above the class. SpanStart sits after the
        // class's leading trivia (before any existing attribute lists), and our plugins are
        // file-scoped, so the class is at column 0 - no indentation needed.
        if (infoAttr is null)
        {
            yield return new TextChange(
                new TextSpan(ctx.PluginClass.SpanStart, 0),
                $"[Info(\"{expandedName}\", \"hizen\", \"{ctx.Options.Version}\")]\n");
            yield break;
        }

        // Otherwise rewrite the existing args in place: Info("<name>", "<author>", "<version>").
        var nameArg = infoAttr.ArgumentList?.Arguments.ElementAtOrDefault(0);
        var authorArg = infoAttr.ArgumentList?.Arguments.ElementAtOrDefault(1);
        var versionArg = infoAttr.ArgumentList?.Arguments.ElementAtOrDefault(2);

        if (nameArg is not null)
            yield return new TextChange(nameArg.Span, $"\"{expandedName}\"");

        if (authorArg is not null)
            yield return new TextChange(authorArg.Span, $"\"hizen\"");

        if (versionArg is not null)
            yield return new TextChange(versionArg.Span, $"\"{ctx.Options.Version}\"");
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
