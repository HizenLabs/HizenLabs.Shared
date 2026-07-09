# Authoring a HizenLabs plugin

How to structure a plugin so the bundler produces a clean single-file `.cs` for both Carbon and
Oxide. These are conventions, not ceremony - the bundler enforces most of them implicitly.

## The shape

- A plugin is **one public class** deriving from `PluginBase` (the shared marker base).
- It lives in a **neutral namespace** you choose (e.g. `HizenLabs.Plugins.MyPlugin`). Never write
  `Carbon.Plugins` / `Oxide.Plugins` yourself - the bundler emits the
  `#if CARBON ... #else ... #endif` namespace split for you.
- It depends on **HizenLabs.Shared only**. No direct Carbon/Oxide reference, no Carbon/Oxide
  `using`. Anything platform-specific is reached through a shared helper that hides the `#if`
  internally.
- Version is a placeholder at author time: `[Info("Name", "author", "")]`. The release workflow
  stamps the real version; source keeps `""`.

```csharp
using HizenLabs.Shared;

namespace HizenLabs.Plugins.MyPlugin;

[Info("My Plugin", "hizen", "")]
[Description("What it does.")]
public class MyPlugin : PluginBase
{
    // ...
}
```

## Split across files - reference, don't `partial`

You do **not** need partial classes to split a plugin across files. Define each type in its own
file in a sub-namespace under the plugin folder; reference it from the plugin; the bundler inlines
every type that's actually reached (reachability tree-shaking) as a `private` nested member, and
drops the now-dead `using`s.

```
MyPlugin/
  MyPlugin.cs                 // the public plugin class (entry)
  Model/                      // ns HizenLabs.Plugins.MyPlugin.Model
    Thing.cs   Other.cs
  Engine/                     // ns HizenLabs.Plugins.MyPlugin.Engine
    Service.cs
```

Rules that keep this working:

- **Helper types are `internal`** (or otherwise non-public). The bundler treats the *first public
  type* as the plugin class; a second public type would be mistaken for it.
- **Unreferenced types are dropped.** If a type isn't reached from the plugin, it won't be in the
  output - dead code stays out of the bundle.
- **Partial-ing the plugin class across files works.** Part files in the plugin's folder ship as
  sibling top-level partial declarations of the plugin class and are never tree-shaken (a part
  holding only hook methods still ships). Partial shared types keep every part too, emitted as
  nested partials.
- The marker base `PluginBase` is **aliased, not inlined** - the bundle keeps `: PluginBase` and
  prepends a platform-split `using PluginBase = Carbon.Plugins.CarbonPlugin;` /
  `Oxide.Plugins.RustPlugin;` alias (marker-alias transform); the dev-time
  `using HizenLabs.Shared;` is removed.

## Platform-specific behavior

Plugins stay platform-agnostic; the Carbon-vs-Oxide split lives in shared helpers that do the `#if`
internally. If you need something a shared helper doesn't expose yet, add it to HizenLabs.Shared
(with the `#if CARBON` region there), not to the plugin. Neutral game/Unity types (e.g.
`BasePlayer`) are the game itself, not a framework, and may be used directly.

## What the bundler emits

For each plugin: the public class, with every reachable helper (yours + shared) inlined as
`private` nested types, the namespace and base class split by `#if CARBON`, and shared `using`s
stripped. See `tests/fixtures/foo-demo/` for a worked example and its `expected.bundled.cs`.

## Checklist

- [ ] One public class, derives from `PluginBase`, neutral namespace.
- [ ] Depends on HizenLabs.Shared only; no Carbon/Oxide `using`.
- [ ] Helper types are `internal`, one per file, in sub-namespaces.
- [ ] No partial classes for the plugin; split by reference instead.
- [ ] `[Info(... "")]` version left blank for the release workflow.
