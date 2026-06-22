# HizenLabs.Shared

The shared runtime library and build SDK behind HizenLabs plugins.

Rust plugins have to ship as a single `.cs` file, which usually means copying the
same boilerplate (localization, config, UI, pooling) into every plugin. This repo
removes that: you write a normal multi-project solution against a shared library,
and the SDK turns it into the single-file plugins the server expects.

## What's here

| Project | Target | Role |
|---|---|---|
| `HizenLabs.Shared` | net48 | Runtime library: localization, logging, pooling, serialization, UI |
| `HizenLabs.CodeGen` | netstandard2.0 | Roslyn source generators for declarative config, localization, and UI |
| `HizenLabs.Bundler` | net10.0 | Merges a plugin plus the shared code it uses into one self-contained `.cs` |

The generators give you ordinary, IDE-friendly C# (full IntelliSense, navigation,
tests). The bundler inlines only the shared code a plugin actually references, so
each output stays lean.

## Building

The game and framework assemblies come from the test-env: start it once and each
server exports its managed set to `test-env/docker/servers/<name>/refs`.

```powershell
.\test-env\start.ps1   # boots the docker matrix; exports refs on container start

# Build a target: Configuration {Release|Staging} x Platform {Carbon|Oxide}
dotnet build HizenLabs.Shared.slnx -p:Configuration=Release -p:Platform=Carbon
```

Carbon assemblies come from the [`Carbon.Community`](https://www.nuget.org/packages/Carbon.Community)
NuGet package. The Rust game DLLs are never committed; they are fetched locally into
a git-ignored `managed/`.

## License and contributing

HizenLabs.Shared is (c) 2026 Aerial Byte LLC (operating the HizenLabs brand) and is
**dual-licensed**:

- **[GPLv3](LICENSE)** for open-source use. Use it freely in your own GPL-licensed
  plugins.
- **Commercial license** for proprietary/closed plugins. Want to ship
  HizenLabs.Shared in a paid or closed-source plugin? Reach out via
  [hizen.dev](https://hizen.dev).

Contributions are welcome and require our [CLA](CLA.md); see
[CONTRIBUTING.md](CONTRIBUTING.md). The CLA is what keeps the dual-licensing
(and therefore the premium line) possible.

Part of the [HizenLabs](https://github.com/HizenLabs) org.
