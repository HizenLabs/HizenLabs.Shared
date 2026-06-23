# Writing buildkit transforms

The bundler turns an authored plugin into the single `.cs` the server loads. It runs in two
phases:

1. **Inline** - reachable shared types are merged into the plugin class as `private` nested
   members (reachability tree-shaking). The author's neutral namespace is kept here, and the
   marker base (`PluginBase`) is deliberately *not* inlined - it is swapped in phase 2. One
   structural step; lives in `Bundler.cs`.
2. **Transform** - a pipeline of small, independent edits applied to the inlined source. This is
   where the `#if CARBON` split is produced (namespace, base class, ...). This is the part you
   extend.

`Bundler.Bundle` runs both phases and, when given a server's managed references
(`--carbon-refs` / `--oxide-refs`), compile-checks the emitted file under each platform. See
`tests/fixtures/foo-demo/` for a worked plugin and its `expected.bundled.cs` output.

A transform is edit-based on purpose: its output isn't a single valid syntax tree (it carries
both halves of an `#if`), so it returns `TextChange`s rather than a rewritten tree.

## Anatomy

```csharp
public sealed class MyTransform : IPluginTransform
{
    public string Name => "my-transform";   // also the golden-case folder name

    public IEnumerable<TextChange> GetChanges(TransformContext ctx)
    {
        // Use ctx.Root / ctx.PluginClass / ctx.Namespace to find a span,
        // then yield a TextChange replacing it. Return nothing for a no-op.
    }
}
```

`TransformContext` gives you the parsed `Root`, the `SourceText`, the resolved `PluginClass`
(first public type) and `Namespace`, plus `Options` (the Carbon/Oxide names). It's purely
syntactic - no semantic model, no game references needed.

## Add one (test-driven)

1. Write the class above; register it in `TransformPipeline.Default`.
2. Create `tests/HizenLabs.Bundler.Tests/cases/<Name>/` with:
   - `input.cs`  - a plugin before your transform
   - `expected.cs` - exactly what it should become
   Write `expected.cs` first (the output you want), then make the transform produce it.
3. `dotnet test` - the harness finds the case by folder name, runs that transform, and compares
   (ignoring trailing whitespace and CRLF vs LF).

That's the whole loop. Each transform stays ~20-40 lines and is testable in isolation; you never
have to understand the rest of the bundler to add one.

## Worked examples

- `NamespaceTransform` - replaces the file-scoped namespace with the `#if CARBON` split.
- `BaseClassTransform` - swaps `: PluginBase` for `#if CARBON : CarbonPlugin #else : RustPlugin`.
