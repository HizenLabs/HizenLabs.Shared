# Bundler roadmap (design)

The inline -> transform pipeline and per-platform compile-check are done (see `transforms.md`).
Two pieces remain to take a plugin from source to a shipped artifact. This is a design sketch, not
committed behavior yet.

## 1. Deploy -> bundler

Today `DeployPlugins` (in `Directory.Build.props`) copies **raw** plugin source into the active
server's `plugins/` folder. It should instead run the bundler and copy the **bundled** single
`.cs`, so the server hot-loads the same artifact players get.

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

## 2. Release workflow

Version is a placeholder in source (`[Info(... "")]`). A release stamps it and publishes the
bundled `.cs` as the distributable.

Sketch:

- Trigger on a GitHub Release (tag = version) or a tag push.
- For each plugin: bundle (with refs, compile-checked for both platforms), then **stamp the
  version** into the `[Info]` attribute of the emitted `.cs` (the registry of record is the
  release/tag, not source).
- Attach each `<PluginName>.cs` as a **release asset**.
- The single `.cs` carries both platforms (`#if CARBON`), so one asset serves Carbon and Oxide.

Open decisions:

- Version source: the Git tag, or a per-plugin version file? (Tag is simplest for a mono-release;
  per-plugin needs independent versioning.)
- Stamp at emit time (string-replace the empty `[Info]` version) vs. a bundler `--version` flag
  that writes it during the namespace/base transforms pass. (A `version` transform is the clean
  home and stays testable.)
- Changelog/notes source - release body, or generated from commits since last tag.

## Suggested order

1. Deploy -> bundler (unblocks real iteration: edit plugin, build, see the bundled `.cs` hot-load).
2. A `version` transform + `--version` flag (small, testable, reused by release).
3. Release workflow (wraps 1 + 2 in CI and publishes assets).
