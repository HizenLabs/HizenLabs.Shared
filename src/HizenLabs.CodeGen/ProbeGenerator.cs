using Microsoft.CodeAnalysis;

namespace HizenLabs.CodeGen;

/// <summary>
/// Placeholder incremental generator that proves the generator pipeline is wired into a
/// consuming plugin build. Replaced by the real declarative generators ([Config],
/// localization, UI tables) in Phase 4.
/// </summary>
[Generator]
public sealed class ProbeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(
                "HizenLabs.CodeGen.Probe.g.cs",
                "// HizenLabs.CodeGen generator pipeline online.\n"));
    }
}
