using Newtonsoft.Json;
using UnityEngine;

namespace HizenLabs.Shared;

/// <summary>
/// Smoke test proving the net48 build resolves the Rust game assemblies (UnityEngine,
/// Newtonsoft.Json from the managed reference set). Replaced by the real shared runtime
/// (Localizer, Logs, Pooling, Serialization, UI, Material) in Phase 3.
/// </summary>
internal static class BuildProbe
{
    public static string Describe(Vector3 v) =>
        JsonConvert.SerializeObject(new { v.x, v.y, v.z });
}
