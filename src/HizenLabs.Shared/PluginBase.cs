namespace HizenLabs.Shared;

/// <summary>
/// The base every HizenLabs plugin derives from. It resolves to the platform's plugin base so
/// your dev code compiles under both targets, and the bundler swaps <c>: PluginBase</c> for the
/// concrete base in the deployed file (see BaseClassTransform). You write <c>: PluginBase</c>;
/// the server sees <c>: CarbonPlugin</c> or <c>: RustPlugin</c>.
/// </summary>
#if CARBON
public abstract class PluginBase : Carbon.Plugins.CarbonPlugin { }
#else
public abstract class PluginBase : Oxide.Plugins.RustPlugin { }
#endif
