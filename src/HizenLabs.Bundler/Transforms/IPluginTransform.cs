using Microsoft.CodeAnalysis.Text;

namespace HizenLabs.Bundler.Transforms;

/// <summary>
/// One source-level rewrite applied to a plugin on its way to a deployable .cs.
///
/// A transform locates what it cares about via the parsed tree on <see cref="TransformContext"/>
/// and returns text edits; the pipeline applies them. We stay edit-based (rather than returning a
/// new syntax tree) on purpose: the output of these transforms is not a single valid tree - it
/// contains both halves of an <c>#if CARBON ... #else ... #endif</c> split.
///
/// To add a transform: implement this interface, register it in
/// <see cref="TransformPipeline.Default"/>, and add a golden case under
/// tests/HizenLabs.Bundler.Tests/cases/&lt;Name&gt;/ (input.cs + expected.cs).
/// </summary>
public interface IPluginTransform
{
    /// <summary>Stable id; also the golden-test case folder name.</summary>
    string Name { get; }

    /// <summary>Edits to apply, computed against the original source. May be empty (no-op).</summary>
    IEnumerable<TextChange> GetChanges(TransformContext context);
}
