using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// Applies a set of <see cref="IPluginTransform"/> to a source string and returns the result.
/// All edits are computed against the original text and applied together, so transforms stay
/// independent (order only matters when two would touch the same span - ours don't).
/// </summary>
public static class TransformPipeline
{
    /// <summary>The transforms every plugin goes through, in order.</summary>
    public static readonly IReadOnlyList<IPluginTransform> Default =
    [
        new BaseClassTransform(),
        new NamespaceTransform(),
        new VersionTransform(),
    ];

    public static string Run(
        string source,
        IReadOnlyList<IPluginTransform>? transforms = null,
        TransformOptions? options = null)
    {
        transforms ??= Default;
        var ctx = TransformContext.Create(source, options);

        var changes = transforms
            .SelectMany(t => t.GetChanges(ctx))
            .OrderBy(c => c.Span.Start)
            .ToList();

        return changes.Count == 0 ? source : ctx.Text.WithChanges(changes).ToString();
    }
}
