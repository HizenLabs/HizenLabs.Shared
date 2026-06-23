# Bundler roadmap (design)

The inline -> transform pipeline and per-platform compile-check are done (see `transforms.md`).
Steps 1 and 2 below are now implemented; step 3 (release workflow) remains a design sketch.

## 1. Deploy -> bundler (DONE)

`DeployPlugins` (in `Directory.Build.props`) now runs the bundler and copies the **bundled** single
`.cs` into the active server's `plugins/` folder, so the server hot-loads the same artifact players
get. It bundles to an intermediate staging dir, compile-checks against the active server's managed
refs, and only copies on success - a compile failure fails the build and leaves the staged `.cs`
(with navigable errors) for inspection rather than shipping a broken file. The `--shared-dir` flag
is passed twice (shared runtime + the plugin's own folder) so plugin-local helpers get inlined.

Original sketch, for reference:

What it needs to pass the bundler:

- **Entry**: the plugin's public-class file (`--plugin`).
- **Inlinable set**: the plugin's own folder `.cs` files **plus** the shared runtime source, all as
  the set the bundler tree-shakes from. (Reachability already inlines plugin-folder sub-namespace
  types - verified - so deploy just has to include the folder.)
- **Refs** (optional): the active server's exported managed dir as `--carbon-refs`/`--oxide-refs`
  so deploy fails fast on a plugin that won't compile, instead of shipping a broken `.cs`.
- **Out**: `servers/<name>/plugins/<PluginName>.cs`.

Open decisions:

- Invoke the bundler as an MSBuild task/exec inside `DeployPlugins`, or a small wrapper target that
  enumerates the plugin folder? (Exec is simplest; a task gives better incremental build.)
- One entry per plugin folder - infer the entry as the single public class, or require a marker?
  (Inferring matches the bundler's existing "first public type" rule.)
- Deploy-time compile-check on by default (slower, safer) or opt-in?

## 2. Version transform (DONE)

The `version` transform stamps `[Info(name, "hizen", version)]` on the plugin class - rewriting an
existing `[Info]` or inserting one when absent. Version is `YY.MM.<minutes-in-month>`
(`PluginVersion.ForDate`), always parseable by the `System.Version` Oxide/Carbon run over the
attribute, and monotonic. It is computed from the clock at bundle time, not passed via a flag.

## 3. Release workflow

A release publishes the bundled `.cs` as the distributable.

The `[Info]` version is already stamped at bundle time (`YY.MM.<minutes-in-month>`, step 2), so the
release does not source or stamp a version - the bundled `.cs` is the artifact as-is.

Sketch:

- Trigger on a GitHub Release or a tag push.
- For each plugin: bundle (with refs, compile-checked for both platforms).
- Attach each `<PluginName>.cs` as a **release asset**.
- The single `.cs` carries both platforms (`#if CARBON`), so one asset serves Carbon and Oxide.

Open decisions:

- How the release tag/name relates to the baked-in `YY.MM.<minutes-in-month>` version (the tag
  records/derives from it rather than overriding it).
- Changelog/notes source - release body, or generated from commits since last tag.
