namespace HizenLabs.Shared;

/// <summary>
/// The base every HizenLabs plugin derives from. It resolves to the platform's plugin base so
/// your dev code compiles under both targets. In the deployed file the bundler keeps every
/// <c>PluginBase</c> reference as written and prepends a platform-split
/// <c>using PluginBase = Carbon.Plugins.CarbonPlugin;</c> / <c>Oxide.Plugins.RustPlugin;</c>
/// alias (see MarkerAliasTransform), so the server resolves it to the concrete base.
/// </summary>
#if CARBON
public abstract class PluginBase : CarbonPlugin { }
#else
public abstract class PluginBase : RustPlugin { }
#endif
