/// <summary>
/// Marks the handler of a menu button command. Carbon resolves it through the
/// protected-command table (buttons emit Community.Protect of the same string); Oxide runs it
/// as a plain console command.
///
/// DEV-TIME ONLY: this declaration never ships. MenuCommandAttribute is a bundler marker type
/// (see TypeMarker in the bundler) - the emitted plugin carries a platform-split using alias
/// to ProtectedCommandAttribute / Oxide.Plugins.ConsoleCommandAttribute instead, the same way
/// PluginBase resolves to the platform base. The base list below only makes dev compiles see
/// the right member surface per platform.
/// </summary>
public class MenuCommandAttribute :
#if CARBON
    ProtectedCommandAttribute
#else
    ConsoleCommandAttribute
#endif
{
    public MenuCommandAttribute(string command) : base(command) { }
}
